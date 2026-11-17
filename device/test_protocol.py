"""Tests for protocol.py. Run from device/: python -m pytest -v"""

import pytest

from protocol import (
    Clock,
    Config,
    Message,
    ProtocolError,
    encode_press,
    parse_clock,
    parse_config,
    parse_message,
)

SIXTEEN = "A" * 16
SEVENTEEN = "A" * 17


# --- message -------------------------------------------------------------------


def test_message_protocol_example():
    assert parse_message(b"Take meds|With breakfast|1|0") == Message(
        "Take meds", "With breakfast", 1, 0
    )


def test_message_accepts_bytearray():
    # bless hands on_write a bytearray, not bytes.
    assert parse_message(bytearray(b"a|b|0|2")).hold == 2


def test_message_empty_lines_allowed():
    assert parse_message(b"||0|0") == Message("", "", 0, 0)


def test_message_sixteen_chars_allowed():
    assert parse_message(f"{SIXTEEN}|{SIXTEEN}|0|0".encode()).line1 == SIXTEEN


def test_message_all_printable_ascii_except_pipe():
    allowed = "".join(chr(c) for c in range(32, 127) if chr(c) != "|")
    for i in range(0, len(allowed), 16):
        chunk = allowed[i:i + 16]
        assert parse_message(f"{chunk}||0|0".encode()).line1 == chunk


@pytest.mark.parametrize(
    "payload",
    [
        b"",                               # no fields
        b"Take meds|With breakfast|1",     # too few fields
        b"Take meds|With breakfast|1|0|0",  # too many fields (or a | in text)
    ],
)
def test_message_wrong_field_count(payload):
    with pytest.raises(ProtocolError):
        parse_message(payload)


def test_message_text_too_long():
    with pytest.raises(ProtocolError):
        parse_message(f"{SEVENTEEN}|b|0|0".encode())
    with pytest.raises(ProtocolError):
        parse_message(f"a|{SEVENTEEN}|0|0".encode())


@pytest.mark.parametrize(
    "payload",
    [
        "Café|b|0|0".encode("utf-8"),  # non-ASCII byte
        "😀|b|0|0".encode("utf-8"),    # emoji
        b"a\tb|c|0|0",                 # tab: ASCII but not printable
        b"a\x00|c|0|0",                # control character
        b"a\x7f|c|0|0",                # DEL (127)
    ],
)
def test_message_disallowed_characters(payload):
    with pytest.raises(ProtocolError):
        parse_message(payload)


@pytest.mark.parametrize(
    "beep, hold",
    [
        ("2", "0"),   # beep out of range
        ("0", "3"),   # hold out of range
        ("-1", "0"),  # negative
        ("", "0"),    # empty number
        ("x", "0"),   # not a number
        (" 1", "0"),  # spaces (int() would accept this)
        ("+1", "0"),  # sign (int() would accept this)
        ("1.0", "0"),
    ],
)
def test_message_bad_numbers(beep, hold):
    with pytest.raises(ProtocolError):
        parse_message(f"a|b|{beep}|{hold}".encode())


# --- clock ---------------------------------------------------------------------


def test_clock_protocol_example():
    assert parse_clock(b"1793122440|-240") == Clock(1793122440, -240)


def test_clock_positive_offset():
    assert parse_clock(b"1793122440|330").offset_minutes == 330


@pytest.mark.parametrize(
    "payload",
    [
        b"1793122440",           # too few fields
        b"1793122440|-240|0",    # too many fields
        b"-5|0",                 # negative epoch
        b"17931.5|0",            # not a whole number
        b"1793122440|",          # empty offset
        b"1793122440|--240",
        b"1793122440|+240",
    ],
)
def test_clock_rejects(payload):
    with pytest.raises(ProtocolError):
        parse_clock(payload)


# --- config --------------------------------------------------------------------


def test_config_protocol_example():
    assert parse_config(b"Take Meds|TAKE MEDS|Due 8:00 AM|12|1") == Config(
        "Take Meds", "TAKE MEDS", "Due 8:00 AM", 12, 1
    )


def test_config_reset_hours_bounds():
    assert parse_config(b"n|||1|0").reset_hours == 1
    assert parse_config(b"n|||168|0").reset_hours == 168


def test_config_empty_rest_lines_allowed():
    assert parse_config(b"n|||12|0") == Config("n", "", "", 12, 0)


@pytest.mark.parametrize(
    "payload",
    [
        b"Take Meds|a|b|12",          # too few fields
        b"Take Meds|a|b|12|1|0",      # too many fields
        b"|a|b|12|1",                 # empty name (needs 1-16)
        f"{SEVENTEEN}|a|b|12|1".encode(),
        f"n|{SEVENTEEN}|b|12|1".encode(),
        f"n|a|{SEVENTEEN}|12|1".encode(),
        "Café|a|b|12|1".encode("utf-8"),
        b"n|a|b|0|1",                 # resetHours below 1
        b"n|a|b|169|1",               # resetHours above 168
        b"n|a|b|12|2",                # highPriority out of range
        b"n|a|b|twelve|1",
    ],
)
def test_config_rejects(payload):
    with pytest.raises(ProtocolError):
        parse_config(payload)


# --- press ---------------------------------------------------------------------


@pytest.mark.parametrize("count, expected", [(0, b"0"), (7, b"7"), (1234, b"1234")])
def test_encode_press(count, expected):
    assert encode_press(count) == expected


def test_encode_press_negative():
    with pytest.raises(ProtocolError):
        encode_press(-1)
