"""Atlist BLE server: advertise, validate writes, forward to the Bridge, notify presses (steps 1-4).

Runs on the UNO Q's Linux side. See docs/Protocol.md for the contract.
"""

import asyncio
import logging
import re
import subprocess

from bless import (
    BlessServer,
    BlessGATTCharacteristic,
    GATTCharacteristicProperties,
    GATTAttributePermissions,
)
from dbus_next.errors import DBusError

from bridge import Bridge, FakeBridge
from protocol import (
    Message,
    ProtocolError,
    encode_press,
    parse_clock,
    parse_config,
    parse_message,
)

# UUIDs from docs/Protocol.md
SERVICE_UUID = "101ef69f-fe23-4d35-8a19-caa905720000"
MESSAGE_UUID = "101ef69f-fe23-4d35-8a19-caa905720001"
PRESS_UUID = "101ef69f-fe23-4d35-8a19-caa905720002"
CLOCK_UUID = "101ef69f-fe23-4d35-8a19-caa905720003"
CONFIG_UUID = "101ef69f-fe23-4d35-8a19-caa905720004"

# For printing, so the log says "message" instead of a long UUID.
NAMES = {
    MESSAGE_UUID: "message",
    PRESS_UUID: "press",
    CLOCK_UUID: "clock",
    CONFIG_UUID: "config",
}

# Which parser checks each writable characteristic.
PARSERS = {
    MESSAGE_UUID: parse_message,
    CLOCK_UUID: parse_clock,
    CONFIG_UUID: parse_config,
}

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Set in main(). on_write is a plain callback from bless, so it reaches the
# bridge and the loop through these.
bridge: Bridge | None = None
loop: asyncio.AbstractEventLoop | None = None

# Strong references to running bridge tasks, so they aren't garbage
# collected before they finish. Each removes itself when done.
_tasks: set[asyncio.Task] = set()


def bluetooth_address() -> str:
    """Return the adapter's address, e.g. '14:B5:CD:F0:F2:79'.

    Reads the 'Controller XX:XX:XX:XX:XX:XX' line from `bluetoothctl show`.
    """
    out = subprocess.run(
        ["bluetoothctl", "show"], capture_output=True, text=True, check=True
    ).stdout
    match = re.search(r"Controller ((?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2})", out)
    if not match:
        raise RuntimeError(f"No Bluetooth controller found in:\n{out}")
    return match.group(1)


def device_name(address: str) -> str:
    """'AA:BB:CC:DD:3F:2A' -> 'Checklist-3F2A' (Protocol.md, Device name)."""
    last4 = address.replace(":", "")[-4:].upper()
    return f"Checklist-{last4}"


def label(characteristic: BlessGATTCharacteristic) -> str:
    uuid = str(characteristic.uuid).lower()
    return NAMES.get(uuid, uuid)


def on_read(characteristic: BlessGATTCharacteristic) -> bytearray:
    """bless calls this whenever a phone reads a characteristic."""
    logger.info(f"Read {label(characteristic)} -> {characteristic.value!r}")
    return characteristic.value


def on_write(characteristic: BlessGATTCharacteristic, value: bytearray) -> None:
    """bless calls this whenever a phone writes a characteristic.

    The payload is parsed first. If it breaks a Protocol.md rule, nothing is
    stored and a DBusError named org.bluez.Error.Failed is raised. bless
    passes it up to dbus-next, which replies to BlueZ with that error, and
    BlueZ fails the phone's write with ATT error 0x80. Any other error name
    (a plain ProtocolError, for example) is reported to the phone as a
    successful write.
    """
    name = label(characteristic)
    parser = PARSERS.get(str(characteristic.uuid).lower())
    try:
        if parser is None:
            raise ProtocolError(f"{name} is not writable")
        parsed = parser(value)
    except ProtocolError as e:
        logger.warning(f"Rejected {name} write {bytes(value)!r}: {e}")
        raise DBusError("org.bluez.Error.Failed", str(e)) from e
    characteristic.value = value
    print(f"Write {name}: {parsed}", flush=True)

    if isinstance(parsed, Message):
        _run(bridge.show(parsed.line1, parsed.line2, parsed.beep, parsed.hold))


def _run(coro) -> None:
    """Start a bridge call from a sync callback and log its outcome.

    The BLE write has already succeeded by now: the payload was valid, and
    a slow or failed sketch shouldn't hold up or fail the phone's write.
    """
    task = loop.create_task(coro)
    _tasks.add(task)
    task.add_done_callback(_finished)


def _finished(task: asyncio.Task) -> None:
    _tasks.discard(task)
    if task.cancelled():
        return
    if (e := task.exception()) is not None:
        logger.error(f"Bridge call failed: {e!r}")
    elif task.result() is not True:
        logger.warning(f"Bridge call returned {task.result()!r}")


def notify_press(server: BlessServer, count: int) -> None:
    """Set `press` to the new count and notify subscribed phones.

    The bridge calls this on the asyncio loop's thread, so touching bless
    here is safe. In bless 0.3.0, update_value() pushes the characteristic's
    stored value to subscribers, so set .value first.
    """
    press = server.get_characteristic(PRESS_UUID)
    press.value = bytearray(encode_press(count))
    if server.update_value(SERVICE_UUID, PRESS_UUID):
        logger.info(f"Notify press -> {count}")
    else:
        logger.error("Notify press failed: service not found")


async def main() -> None:
    global bridge, loop
    loop = asyncio.get_running_loop()
    bridge = FakeBridge()
    bridge.start(loop)

    name = device_name(bluetooth_address())
    server = BlessServer(name=name, loop=loop)
    server.read_request_func = on_read
    server.write_request_func = on_write

    await server.add_new_service(SERVICE_UUID)

    # Phone -> device: write only.
    for uuid in (MESSAGE_UUID, CLOCK_UUID, CONFIG_UUID):
        await server.add_new_characteristic(
            SERVICE_UUID,
            uuid,
            GATTCharacteristicProperties.write,
            None,
            GATTAttributePermissions.writeable,
        )

    # Device -> phone: read + notify, starting at "0".
    await server.add_new_characteristic(
        SERVICE_UUID,
        PRESS_UUID,
        GATTCharacteristicProperties.read | GATTCharacteristicProperties.notify,
        bytearray(b"0"),
        GATTAttributePermissions.readable,
    )

    await server.start()
    bridge.on_pressed(lambda count: notify_press(server, count))
    logger.info(f"Advertising as {name}. Ctrl+C to stop.")

    try:
        while True:
            await asyncio.sleep(1)
    finally:
        # Runs on Ctrl+C too, so BlueZ is left clean.
        await server.stop()
        logger.info("Stopped.")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
