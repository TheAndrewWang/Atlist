"""The Bridge interface between the BLE server and the sketch (step 3).

docs/Protocol.md, "Bridge (Linux <-> MCU)", defines the calls. The BLE server
only talks to a `Bridge`, so `FakeBridge` (prints, keyboard for the button)
can stand in for `RealBridge` (step 6, talks to the sketch).

Threading rule: handlers registered with `on_pressed` / `on_pair_hold` are
always called on the asyncio loop's thread, so they can touch bless directly.
"""

import asyncio
import itertools
import logging
import os
import sys
import threading
from abc import ABC, abstractmethod
from typing import Any, Callable, Optional

import msgpack

logger = logging.getLogger(__name__)

PressedHandler = Callable[[int], None]
PairHoldHandler = Callable[[], None]


class Bridge(ABC):
    """Python -> sketch calls are async. Each returns the sketch's bool."""

    def __init__(self) -> None:
        self._pressed_handler: Optional[PressedHandler] = None
        self._pair_hold_handler: Optional[PairHoldHandler] = None

    # --- Python -> sketch -------------------------------------------------

    @abstractmethod
    async def show(self, line1: str, line2: str, beep: int, hold: int) -> bool: ...

    @abstractmethod
    async def rest(self, line1: str, line2: str) -> bool: ...

    @abstractmethod
    async def pair_show(self, passkey: str) -> bool: ...

    @abstractmethod
    async def pair_end(self) -> bool: ...

    @abstractmethod
    async def clear_done(self) -> bool: ...

    # --- Sketch -> Python -------------------------------------------------

    def on_pressed(self, handler: PressedHandler) -> None:
        """`handler(count)` runs on each short press."""
        self._pressed_handler = handler

    def on_pair_hold(self, handler: PairHoldHandler) -> None:
        """`handler()` runs when the button is held for 3 seconds."""
        self._pair_hold_handler = handler

    def _emit_pressed(self, count: int) -> None:
        if self._pressed_handler is not None:
            self._pressed_handler(count)

    def _emit_pair_hold(self) -> None:
        if self._pair_hold_handler is not None:
            self._pair_hold_handler()

    # --- Lifecycle --------------------------------------------------------

    def start(self, loop: asyncio.AbstractEventLoop) -> None:
        """Begin delivering sketch -> Python events on `loop`."""
        self._loop = loop


class FakeBridge(Bridge):
    """Prints every call. Type `p` + Enter for a press, `h` for a 3 s hold."""

    def __init__(self) -> None:
        super().__init__()
        self._count = 0  # Protocol.md: the sketch counts presses since boot.

    def _log(self, name: str, *args: object) -> bool:
        print(f"{name}({', '.join(repr(a) for a in args)})", flush=True)
        return True

    async def show(self, line1: str, line2: str, beep: int, hold: int) -> bool:
        return self._log("show", line1, line2, beep, hold)

    async def rest(self, line1: str, line2: str) -> bool:
        return self._log("rest", line1, line2)

    async def pair_show(self, passkey: str) -> bool:
        return self._log("pair_show", passkey)

    async def pair_end(self) -> bool:
        return self._log("pair_end")

    async def clear_done(self) -> bool:
        return self._log("clear_done")

    def start(self, loop: asyncio.AbstractEventLoop) -> None:
        super().start(loop)
        # input() blocks, so read stdin on its own thread and hand each line
        # to the loop, the same way RealBridge's handlers will arrive from
        # Bridge.read_loop's thread.
        threading.Thread(target=self._read_keys, daemon=True).start()
        print("FakeBridge: type p + Enter to press, h + Enter to hold.", flush=True)

    def _read_keys(self) -> None:
        for line in sys.stdin:
            self._loop.call_soon_threadsafe(self._handle_key, line.strip().lower())

    def _handle_key(self, key: str) -> None:
        if key == "p":
            self._count += 1
            print(f"FakeBridge: pressed({self._count})", flush=True)
            self._emit_pressed(self._count)
        elif key == "h":
            print("FakeBridge: pair_hold()", flush=True)
            self._emit_pair_hold()
        elif key:
            print(f"FakeBridge: unknown key {key!r}, use p or h", flush=True)


# --- Real Bridge: MessagePack-RPC to arduino-router (step 6) ------------------

# Same default and override as App Lab's arduino.app_utils.Bridge.
DEFAULT_SOCKET = "/var/run/arduino-router.sock"
RECONNECT_DELAY = 3.0  # seconds, as in arduino.app_utils
CALL_TIMEOUT = 10.0  # seconds, Bridge.call's default

# Error codes in a response's [code, message] (arduino.app_utils 0.10.1).
ROUTE_ALREADY_EXISTS_ERR = 0x05
FUNCTION_NOT_FOUND_ERR = 0xFE
GENERIC_ERR = 0xFF

# Message types (MessagePack-RPC).
REQUEST, RESPONSE, NOTIFY = 0, 1, 2


class BridgeError(RuntimeError):
    """The router or sketch answered a call with an error."""

    def __init__(self, method: str, error: Any) -> None:
        self.method = method
        self.code = error[0] if isinstance(error, (list, tuple)) and error else None
        message = error[1] if isinstance(error, (list, tuple)) and len(error) > 1 else error
        super().__init__(f"{method}: {message} (code {self.code})")


def socket_path() -> str:
    """$APP_SOCKET (`unix:///path`) if set, else the router's default socket."""
    address = os.environ.get("APP_SOCKET", "")
    if address.startswith("unix://"):
        return address[len("unix://"):]
    if address:
        raise ValueError(f"Only unix:// APP_SOCKET addresses are supported: {address!r}")
    return DEFAULT_SOCKET


class RealBridge(Bridge):
    """Talks to the sketch through arduino-router's Unix socket.

    The wire format follows App Lab's `arduino.app_utils.Bridge` (0.10.1):
      request      [0, msgid, method, [args]]
      response     [1, msgid, error, result]   error is None or [code, message]
      notification [2, method, [args]]
    To receive the sketch's `pressed` / `pair_hold`, Python first calls
    `$/register` with each name, and again after every reconnect.

    Everything runs on the asyncio loop, so handlers are called on the loop's
    thread with no extra hand-off.
    """

    # Sketch -> Python names this bridge registers with the router.
    INCOMING = ("pressed", "pair_hold")

    def __init__(self, path: Optional[str] = None, timeout: float = CALL_TIMEOUT) -> None:
        super().__init__()
        self._path = path or socket_path()
        self._timeout = timeout
        self._ids = itertools.count(1)
        self._pending: dict[int, asyncio.Future] = {}
        self._writer: Optional[asyncio.StreamWriter] = None
        self._connected = asyncio.Event()
        self._task: Optional[asyncio.Task] = None

    # --- Python -> sketch -------------------------------------------------

    async def show(self, line1: str, line2: str, beep: int, hold: int) -> bool:
        return bool(await self.call("show", line1, line2, beep, hold))

    async def rest(self, line1: str, line2: str) -> bool:
        return bool(await self.call("rest", line1, line2))

    async def pair_show(self, passkey: str) -> bool:
        return bool(await self.call("pair_show", passkey))

    async def pair_end(self) -> bool:
        return bool(await self.call("pair_end"))

    async def clear_done(self) -> bool:
        return bool(await self.call("clear_done"))

    async def call(self, method: str, *args: Any) -> Any:
        """Call `method` on the sketch and return its result.

        Waits for the router connection, then for the reply, all within the
        timeout. Raises TimeoutError, BridgeError, or ConnectionError if the
        connection drops mid-call.
        """
        async with asyncio.timeout(self._timeout):
            await self._connected.wait()
            return await self._request(method, list(args))

    # --- Lifecycle --------------------------------------------------------

    def start(self, loop: asyncio.AbstractEventLoop) -> None:
        super().start(loop)
        self._task = loop.create_task(self._run(), name="RealBridge")

    async def close(self) -> None:
        if self._task is not None:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass

    async def _run(self) -> None:
        """Connect, register, read until the connection drops, repeat."""
        while True:
            try:
                reader, self._writer = await asyncio.open_unix_connection(self._path)
            except OSError as e:
                logger.error(f"Can't reach arduino-router at {self._path}: {e}. "
                             f"Retrying in {RECONNECT_DELAY:.0f} s.")
                await asyncio.sleep(RECONNECT_DELAY)
                continue

            logger.info(f"Connected to arduino-router at {self._path}")
            self._connected.set()
            register = asyncio.create_task(self._register_all())
            try:
                await self._read_loop(reader)
            finally:
                self._connected.clear()
                register.cancel()
                self._fail_pending(ConnectionError("Connection to arduino-router lost"))
                self._writer.close()
                self._writer = None

            logger.warning(f"arduino-router connection lost. Reconnecting in {RECONNECT_DELAY:.0f} s.")
            await asyncio.sleep(RECONNECT_DELAY)

    async def _register_all(self) -> None:
        for name in self.INCOMING:
            try:
                async with asyncio.timeout(self._timeout):
                    await self._request("$/register", [name])
                logger.info(f"Registered {name!r} with arduino-router")
            except BridgeError as e:
                if e.code != ROUTE_ALREADY_EXISTS_ERR:
                    logger.error(f"Couldn't register {name!r}: {e}")
            except (TimeoutError, ConnectionError) as e:
                logger.error(f"Couldn't register {name!r}: {e!r}")

    # --- Wire -------------------------------------------------------------

    async def _request(self, method: str, params: list) -> Any:
        msgid = next(self._ids) % 2**32
        future = asyncio.get_running_loop().create_future()
        self._pending[msgid] = future
        try:
            self._writer.write(msgpack.packb([REQUEST, msgid, method, params]))
            await self._writer.drain()
            error, result = await future
        finally:
            self._pending.pop(msgid, None)
        # Like arduino.app_utils: "route already exists" isn't a failure.
        if error is not None and not (result is None and _code(error) == ROUTE_ALREADY_EXISTS_ERR):
            raise BridgeError(method, error)
        return result

    async def _read_loop(self, reader: asyncio.StreamReader) -> None:
        unpacker = msgpack.Unpacker(raw=False)
        while data := await reader.read(4096):
            unpacker.feed(data)
            for msg in unpacker:
                try:
                    self._handle(msg)
                except Exception:
                    logger.exception(f"Bad message from arduino-router: {msg!r}")

    def _handle(self, msg: Any) -> None:
        if not isinstance(msg, list) or not msg:
            raise ValueError("not a non-empty array")
        kind = msg[0]
        if kind == RESPONSE and len(msg) == 4:
            _, msgid, error, result = msg
            future = self._pending.get(msgid)
            if future is not None and not future.done():
                future.set_result((error, result))
            else:
                logger.warning(f"Response for unknown msgid {msgid}")
        elif kind == NOTIFY and len(msg) == 3:
            _, method, params = msg
            self._dispatch(method, params)
        elif kind == REQUEST and len(msg) == 4:
            # The sketch used Bridge.call instead of Bridge.notify. Handle it
            # the same way, and reply so the sketch isn't left waiting.
            _, msgid, method, params = msg
            if method in self.INCOMING:
                self._dispatch(method, params)
                self._reply(msgid, None, True)
            else:
                self._reply(msgid, [FUNCTION_NOT_FOUND_ERR, f"Method not found: {method!r}"], None)
        else:
            raise ValueError("unknown message shape")

    def _dispatch(self, method: str, params: list) -> None:
        if method == "pressed":
            (count,) = params
            self._emit_pressed(int(count))
        elif method == "pair_hold":
            self._emit_pair_hold()
        else:
            logger.warning(f"Ignoring notification {method!r}{tuple(params)}")

    def _reply(self, msgid: int, error: Any, result: Any) -> None:
        if self._writer is not None:
            self._writer.write(msgpack.packb([RESPONSE, msgid, error, result]))

    def _fail_pending(self, reason: Exception) -> None:
        for future in self._pending.values():
            if not future.done():
                future.set_exception(reason)
        self._pending.clear()


def _code(error: Any) -> Any:
    return error[0] if isinstance(error, (list, tuple)) and error else None
