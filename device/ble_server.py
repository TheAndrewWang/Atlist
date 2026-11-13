"""Atlist BLE server, step 1: advertise and expose the service.

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

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


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

    Step 1 only stores and prints. Step 2 adds validation.
    """
    characteristic.value = value
    text = bytes(value).decode("ascii", errors="replace")
    print(f"Write {label(characteristic)}: {text!r}", flush=True)


async def main() -> None:
    name = device_name(bluetooth_address())
    server = BlessServer(name=name, loop=asyncio.get_running_loop())
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
