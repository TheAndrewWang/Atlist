"""The Bridge interface between the BLE server and the sketch (step 3).

docs/Protocol.md, "Bridge (Linux <-> MCU)", defines the calls. The BLE server
only talks to a `Bridge`, so `FakeBridge` (prints, keyboard for the button)
can stand in for `RealBridge` (step 6, talks to the sketch).

Threading rule: handlers registered with `on_pressed` / `on_pair_hold` are
always called on the asyncio loop's thread, so they can touch bless directly.
"""

import asyncio
import sys
import threading
from abc import ABC, abstractmethod
from typing import Callable, Optional

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
