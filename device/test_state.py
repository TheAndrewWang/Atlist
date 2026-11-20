"""Tests for state.py. Run with `pytest` in device/."""

import json

import pytest

import state as state_mod
from protocol import Clock, Config
from state import State, load_state, save_state

CONFIG = Config(name="Take Meds", rest1="TAKE MEDS", rest2="Due 8:00 AM",
                reset_hours=12, high_priority=1)


# --- Clock ----------------------------------------------------------------------

def test_with_clock_stores_skew_not_system_time():
    s = State().with_clock(Clock(epoch=1_000_100, offset_minutes=-240), now=1_000_000)
    assert s.clock_skew == 100
    assert s.offset_minutes == -240


def test_utc_now_applies_skew():
    s = State().with_clock(Clock(epoch=1793122440, offset_minutes=0), now=1_000_000)
    # 60 s of system time later, the phone's clock has also moved 60 s.
    assert s.utc_now(now=1_000_060).timestamp() == 1793122440 + 60


def test_local_now_uses_offset():
    s = State().with_clock(Clock(epoch=1793122440, offset_minutes=-240), now=0)
    local = s.local_now(now=0)
    assert local.utcoffset().total_seconds() == -240 * 60
    assert local.timestamp() == 1793122440  # same instant, different zone


def test_with_clock_keeps_config():
    s = State(config=CONFIG).with_clock(Clock(epoch=5, offset_minutes=60), now=0)
    assert s.config == CONFIG


def test_with_config_keeps_clock():
    s = State(clock_skew=42.0, offset_minutes=-240).with_config(CONFIG)
    assert (s.clock_skew, s.offset_minutes, s.config) == (42.0, -240, CONFIG)


# --- Save and load ----------------------------------------------------------------

def test_round_trip(tmp_path):
    path = tmp_path / "state.json"
    s = State(clock_skew=12.5, offset_minutes=-240, config=CONFIG)
    save_state(s, path)
    assert load_state(path) == s


def test_round_trip_without_config(tmp_path):
    path = tmp_path / "state.json"
    save_state(State(clock_skew=1.0), path)
    assert load_state(path) == State(clock_skew=1.0)


def test_missing_file_gives_fresh_state(tmp_path):
    assert load_state(tmp_path / "nope.json") == State()


@pytest.mark.parametrize("content", ["{not json", "[]", '{"config": {"name": "x"}}'])
def test_bad_file_gives_fresh_state(tmp_path, content):
    path = tmp_path / "state.json"
    path.write_text(content)
    assert load_state(path) == State()


def test_save_overwrites_and_leaves_no_temp_files(tmp_path):
    path = tmp_path / "state.json"
    save_state(State(clock_skew=1.0), path)
    save_state(State(clock_skew=2.0), path)
    assert load_state(path).clock_skew == 2.0
    assert [p.name for p in tmp_path.iterdir()] == ["state.json"]


def test_failed_save_keeps_old_file(tmp_path, monkeypatch):
    path = tmp_path / "state.json"
    save_state(State(config=CONFIG), path)
    before = path.read_text()

    def boom(*args, **kwargs):
        raise OSError("disk full")

    monkeypatch.setattr(state_mod.json, "dump", boom)
    with pytest.raises(OSError):
        save_state(State(), path)

    assert path.read_text() == before
    assert [p.name for p in tmp_path.iterdir()] == ["state.json"]


def test_file_is_readable_json(tmp_path):
    path = tmp_path / "state.json"
    save_state(State(offset_minutes=-240, config=CONFIG), path)
    data = json.loads(path.read_text())
    assert data["offset_minutes"] == -240
    assert data["config"]["rest1"] == "TAKE MEDS"
