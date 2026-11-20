"""What the BLE server remembers across restarts (step 5).

Pure Python with no Bluetooth, so pytest covers it on any machine.

- Clock: the phone's time is stored as a skew from the system clock. The
  system clock itself is never changed (Protocol.md: Python owns time, and
  other things on the board rely on the system clock).
- Config: the last valid `config` write, so the resting screen comes back
  after a restart.

`State` is frozen. Callers build a new one with `with_clock` / `with_config`,
save it, and only then swap it in, so a failed save changes nothing.
"""

import json
import logging
import os
import tempfile
import time
from dataclasses import asdict, dataclass, replace
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Optional

from protocol import Clock, Config

logger = logging.getLogger(__name__)

STATE_FILE = Path(__file__).with_name("state.json")


@dataclass(frozen=True)
class State:
    # Phone time minus system time, in seconds. 0 until the phone syncs.
    clock_skew: float = 0.0
    # Phone's time zone offset from UTC, in minutes, e.g. -240 for EDT.
    offset_minutes: int = 0
    config: Optional[Config] = None

    def with_clock(self, clock: Clock, now: Optional[float] = None) -> "State":
        """Return a copy synced to a `clock` write received at system time `now`."""
        now = time.time() if now is None else now
        return replace(self, clock_skew=clock.epoch - now,
                       offset_minutes=clock.offset_minutes)

    def with_config(self, config: Config) -> "State":
        return replace(self, config=config)

    def utc_now(self, now: Optional[float] = None) -> datetime:
        """The phone's idea of the current time, in UTC."""
        now = time.time() if now is None else now
        return datetime.fromtimestamp(now + self.clock_skew, tz=timezone.utc)

    def local_now(self, now: Optional[float] = None) -> datetime:
        """'Local now': the phone's time in the phone's time zone."""
        tz = timezone(timedelta(minutes=self.offset_minutes))
        return self.utc_now(now).astimezone(tz)


def _to_json(state: State) -> dict:
    return asdict(state)  # Config becomes a nested dict, or None.


def _from_json(data: dict) -> State:
    if not isinstance(data, dict):
        raise TypeError(f"expected a JSON object, got {type(data).__name__}")
    config = data.get("config")
    return State(
        clock_skew=float(data.get("clock_skew", 0.0)),
        offset_minutes=int(data.get("offset_minutes", 0)),
        config=Config(**config) if config is not None else None,
    )


def load_state(path: Path = STATE_FILE) -> State:
    """Load `path`. A missing or unreadable file gives a fresh `State`.

    A bad file is logged, not raised: the device should still start and wait
    for the phone to send config again.
    """
    try:
        with open(path, encoding="utf-8") as f:
            return _from_json(json.load(f))
    except FileNotFoundError:
        return State()
    except (OSError, ValueError, TypeError) as e:
        logger.error(f"Ignoring unreadable {path}: {e!r}")
        return State()


def save_state(state: State, path: Path = STATE_FILE) -> None:
    """Write `state` to `path` atomically.

    The JSON goes to a temporary file in the same folder, which is flushed to
    disk and then renamed over `path`. A rename within one filesystem is
    atomic, so after a power cut `path` holds either the old or the new
    state, never half of one. Raises OSError if the save fails, and leaves
    the old file as it was.
    """
    path = Path(path)
    fd, tmp = tempfile.mkstemp(dir=path.parent, prefix=f".{path.name}.", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(_to_json(state), f, indent=2)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp, path)
    except BaseException:
        try:
            os.unlink(tmp)
        except FileNotFoundError:
            pass
        raise
