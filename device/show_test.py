"""Step 3 test: call the sketch's "show" and "rest" over Bridge.

Run on the UNO Q's Linux side, after flashing device/sketch:
    ~/atlist-venv/bin/python device/show_test.py                  # checkpoint: Drink water + beep
    ~/atlist-venv/bin/python device/show_test.py show L1 L2 BEEP HOLD
    ~/atlist-venv/bin/python device/show_test.py rest L1 L2

Reuses call() from ping_test.py (same folder).
"""

import sys

from ping_test import call


def main(argv):
    if not argv:
        method, args = "show", ["Drink water", "Glass by sink", 1, 0]
    elif argv[0] == "show" and len(argv) == 5:
        method, args = "show", [argv[1], argv[2], int(argv[3]), int(argv[4])]
    elif argv[0] == "rest" and len(argv) == 3:
        method, args = "rest", [argv[1], argv[2]]
    else:
        sys.exit(__doc__)

    print(f"Calling {method}{tuple(args)}")
    error, result = call(method, *args)
    if error:
        sys.exit(f"Sketch/router returned an error: {error}")
    print(f"Reply: {result!r}")
    print("OK: accepted" if result is True else "Unexpected reply")


if __name__ == "__main__":
    main(sys.argv[1:])
