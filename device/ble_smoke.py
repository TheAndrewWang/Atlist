import asyncio
import logging

from bless import (
    BlessServer,
    BlessGATTCharacteristic,
    GATTCharacteristicProperties,
    GATTAttributePermissions,
)

# UUIDs from docs/Protocol.md
SERVICE_UUID = "101ef69f-fe23-4d35-8a19-caa905720000"
PRESS_UUID = "101ef69f-fe23-4d35-8a19-caa905720002"

# DEBUG shows what bless is doing with BlueZ, useful if start() fails.
logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)


def on_read(characteristic: BlessGATTCharacteristic, **kwargs) -> bytearray:
    """bless calls this whenever a phone reads a characteristic."""
    logger.info(f"Read {characteristic.uuid} -> {characteristic.value!r}")
    return characteristic.value


async def main() -> None:
    server = BlessServer(name="Checklist-TEST", loop=asyncio.get_running_loop())
    server.read_request_func = on_read  # the function itself, no ()

    await server.add_new_service(SERVICE_UUID)
    await server.add_new_characteristic(
        SERVICE_UUID,
        PRESS_UUID,
        GATTCharacteristicProperties.read,
        bytearray(b"0"),  # BLE moves bytes, not str
        GATTAttributePermissions.readable,
    )

    await server.start()
    logger.info("Advertising as Checklist-TEST. Ctrl+C to stop.")

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
