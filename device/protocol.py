"""Atlist BLE payloads: parse and validate (step 2).

Pure Python with no Bluetooth, so it can be tested with pytest on any machine.
The rules come from docs/Protocol.md. Every parser either returns a dataclass
or raises ProtocolError, and ble_server.py turns that into a failed BLE write.
"""

import re
from dataclasses import dataclass

MAX_TEXT = 16  # LCD width (Protocol.md, Rules)
SEPARATOR = "|"

# Digits only: no spaces, no "+", no "1.0". int() alone would accept " 1" and "+1".
_UNSIGNED = re.compile(r"[0-9]+")
_SIGNED = re.compile(r"-?[0-9]+")


class ProtocolError(ValueError):
    """A payload broke a Protocol.md rule. The whole write is rejected."""


@dataclass(frozen=True)
class Message:
    line1: str
    line2: str
    beep: int  # 0 = off, 1 = on
    hold: int  # 0 = until pressed, 1 = 1 hour, 2 = until next reminder


@dataclass(frozen=True)
class Clock:
    epoch: int  # Unix seconds, UTC
    offset_minutes: int  # phone's offset from UTC, e.g. -240 for EDT


@dataclass(frozen=True)
class Config:
    name: str
    rest1: str
    rest2: str
    reset_hours: int  # 1-168
    high_priority: int  # 0 = off, 1 = on


# --- Shared helpers -----------------------------------------------------------


def _split(data: bytes, count: int) -> list[str]:
    """Decode a payload as ASCII and split it into exactly `count` fields."""
    try:
        text = bytes(data).decode("ascii")
    except UnicodeDecodeError:
        raise ProtocolError("payload is not ASCII") from None
    fields = text.split(SEPARATOR)
    if len(fields) != count:
        raise ProtocolError(f"expected {count} fields, got {len(fields)}")
    return fields


def _text(field: str, name: str, min_len: int = 0) -> str:
    """Check a text field: min_len-16 characters, printable ASCII (32-126).

    `|` can't appear here, because _split already used it to cut the fields.
    """
    if not min_len <= len(field) <= MAX_TEXT:
        raise ProtocolError(f"{name} must be {min_len}-{MAX_TEXT} characters")
    for ch in field:
        if not 32 <= ord(ch) <= 126:
            raise ProtocolError(f"{name} has a disallowed character {ch!r}")
    return field


def _number(field: str, name: str, low: int | None, high: int | None,
            signed: bool = False) -> int:
    """Check a decimal number field and that it's in [low, high].

    Pass None for a bound that Protocol.md doesn't set.
    """
    pattern = _SIGNED if signed else _UNSIGNED
    if not pattern.fullmatch(field):
        raise ProtocolError(f"{name} must be a whole number, got {field!r}")
    value = int(field)
    if (low is not None and value < low) or (high is not None and value > high):
        raise ProtocolError(f"{name} out of range: {value}")
    return value


# --- Parsers, one per characteristic ------------------------------------------


def parse_message(data: bytes) -> Message:
    """`line1|line2|beep|hold`, e.g. b"Take meds|With breakfast|1|0"."""
    line1, line2, beep, hold = _split(data, 4)
    return Message(
        line1=_text(line1, "line1"),
        line2=_text(line2, "line2"),
        beep=_number(beep, "beep", 0, 1),
        hold=_number(hold, "hold", 0, 2),
    )


def parse_clock(data: bytes) -> Clock:
    """`epoch|offset`, e.g. b"1793122440|-240".

    Protocol.md gives no range for either field yet, so only the format is
    checked: epoch is a non-negative whole number, offset may be negative.
    """
    epoch, offset = _split(data, 2)
    return Clock(
        epoch=_number(epoch, "epoch", 0, None),
        offset_minutes=_number(offset, "offset", None, None, signed=True),
    )


def parse_config(data: bytes) -> Config:
    """`name|rest1|rest2|resetHours|highPriority`,
    e.g. b"Take Meds|TAKE MEDS|Due 8:00 AM|12|1"."""
    name, rest1, rest2, reset_hours, high_priority = _split(data, 5)
    return Config(
        name=_text(name, "name", min_len=1),
        rest1=_text(rest1, "rest1"),
        rest2=_text(rest2, "rest2"),
        reset_hours=_number(reset_hours, "resetHours", 1, 168),
        high_priority=_number(high_priority, "highPriority", 0, 1),
    )


def encode_press(count: int) -> bytes:
    """Press count as the `press` payload: 7 -> b"7"."""
    if count < 0:
        raise ProtocolError(f"press count can't be negative: {count}")
    return str(count).encode("ascii")
