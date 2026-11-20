"""Tests for RealBridge against a fake arduino-router on a temp Unix socket."""

import asyncio

import msgpack
import pytest

from bridge import (
    FUNCTION_NOT_FOUND_ERR,
    ROUTE_ALREADY_EXISTS_ERR,
    BridgeError,
    RealBridge,
)


class FakeRouter:
    """Answers like arduino-router. `answers[method]` is (error, result)."""

    def __init__(self, path):
        self.path = str(path)
        self.answers = {"$/register": (None, True)}
        self.requests = []  # (method, params) received
        self.writers = []
        self.replies = []  # responses Python sent back to us

    async def start(self):
        self.server = await asyncio.start_unix_server(self._client, self.path)

    async def stop(self):
        for w in self.writers:
            w.close()
        self.server.close()
        await self.server.wait_closed()

    async def _client(self, reader, writer):
        self.writers.append(writer)
        unpacker = msgpack.Unpacker(raw=False)
        while data := await reader.read(4096):
            unpacker.feed(data)
            for msg in unpacker:
                if msg[0] == 0:
                    _, msgid, method, params = msg
                    self.requests.append((method, params))
                    answer = self.answers.get(method)
                    if answer == "silent":
                        continue
                    error, result = answer or ([FUNCTION_NOT_FOUND_ERR, f"no {method}"], None)
                    writer.write(msgpack.packb([1, msgid, error, result]))
                elif msg[0] == 1:
                    self.replies.append(msg)

    def send(self, msg):
        self.writers[-1].write(msgpack.packb(msg))


def run(test):
    """Run `test(router, bridge)` with a connected RealBridge."""
    async def main(tmp_path):
        router = FakeRouter(tmp_path / "router.sock")
        await router.start()
        bridge = RealBridge(path=router.path, timeout=1)
        bridge.start(asyncio.get_running_loop())
        try:
            await asyncio.wait_for(bridge._connected.wait(), 1)
            await settle()  # let the router accept and see the registrations
            await test(router, bridge)
        finally:
            await bridge.close()
            await router.stop()
    return main


async def settle():
    await asyncio.sleep(0.05)


def test_registers_incoming_names(tmp_path):
    async def t(router, bridge):
        await settle()
        assert ("$/register", ["pressed"]) in router.requests
        assert ("$/register", ["pair_hold"]) in router.requests
    asyncio.run(run(t)(tmp_path))


def test_route_already_exists_is_fine(tmp_path, caplog):
    async def t(router, bridge):
        await settle()
        assert "Couldn't register" not in caplog.text
    async def main():
        router = FakeRouter(tmp_path / "r.sock")
        router.answers["$/register"] = ([ROUTE_ALREADY_EXISTS_ERR, "exists"], None)
        await router.start()
        bridge = RealBridge(path=router.path, timeout=1)
        bridge.start(asyncio.get_running_loop())
        await settle()
        await t(router, bridge)
        await bridge.close()
        await router.stop()
    asyncio.run(main())


def test_show_sends_args_and_returns_bool(tmp_path):
    async def t(router, bridge):
        router.answers["show"] = (None, True)
        assert await bridge.show("Drink water", "Glass by sink", 1, 0) is True
        assert ("show", ["Drink water", "Glass by sink", 1, 0]) in router.requests
    asyncio.run(run(t)(tmp_path))


def test_all_calls_use_protocol_names(tmp_path):
    async def t(router, bridge):
        for name in ("rest", "pair_show", "pair_end", "clear_done"):
            router.answers[name] = (None, True)
        assert await bridge.rest("A", "B")
        assert await bridge.pair_show("481732")
        assert await bridge.pair_end()
        assert await bridge.clear_done()
        methods = [m for m, _ in router.requests if not m.startswith("$/")]
        assert methods == ["rest", "pair_show", "pair_end", "clear_done"]
    asyncio.run(run(t)(tmp_path))


def test_sketch_returning_false(tmp_path):
    async def t(router, bridge):
        router.answers["rest"] = (None, False)
        assert await bridge.rest("A", "B") is False
    asyncio.run(run(t)(tmp_path))


def test_missing_function_raises(tmp_path):
    async def t(router, bridge):
        with pytest.raises(BridgeError) as e:
            await bridge.show("a", "b", 0, 0)
        assert e.value.code == FUNCTION_NOT_FOUND_ERR
    asyncio.run(run(t)(tmp_path))


def test_no_reply_times_out(tmp_path):
    async def t(router, bridge):
        router.answers["show"] = "silent"
        with pytest.raises(TimeoutError):
            await bridge.show("a", "b", 0, 0)
        assert bridge._pending == {}
    asyncio.run(run(t)(tmp_path))


def test_pressed_notification_calls_handler(tmp_path):
    async def t(router, bridge):
        got = []
        bridge.on_pressed(got.append)
        router.send([2, "pressed", [3]])
        await settle()
        assert got == [3]
    asyncio.run(run(t)(tmp_path))


def test_pair_hold_notification_calls_handler(tmp_path):
    async def t(router, bridge):
        got = []
        bridge.on_pair_hold(lambda: got.append("hold"))
        router.send([2, "pair_hold", []])
        await settle()
        assert got == ["hold"]
    asyncio.run(run(t)(tmp_path))


def test_pressed_as_request_gets_reply(tmp_path):
    async def t(router, bridge):
        got = []
        bridge.on_pressed(got.append)
        router.send([0, 99, "pressed", [1]])
        await settle()
        assert got == [1]
        assert [1, 99, None, True] in router.replies
    asyncio.run(run(t)(tmp_path))


def test_handler_runs_on_loop_thread(tmp_path):
    import threading
    async def t(router, bridge):
        threads = []
        bridge.on_pressed(lambda c: threads.append(threading.current_thread()))
        router.send([2, "pressed", [1]])
        await settle()
        assert threads == [threading.main_thread()]
    asyncio.run(run(t)(tmp_path))


def test_dropped_connection_fails_pending_call(tmp_path):
    async def t(router, bridge):
        router.answers["show"] = "silent"
        call = asyncio.create_task(bridge.show("a", "b", 0, 0))
        await settle()
        router.writers[-1].close()
        with pytest.raises(ConnectionError):
            await call
    asyncio.run(run(t)(tmp_path))
