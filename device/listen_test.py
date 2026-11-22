"""Step 4 test: print the sketch's "pressed" and "pair_hold" notifications.

Run on the UNO Q's Linux side, after flashing device/sketch:
    ~/atlist-venv/bin/python device/listen_test.py
Then press the button (or type p / h in App Lab's serial monitor). Ctrl+C stops.

Same wire format as the BLE server's RealBridge (device/bridge.py on the
ble-server branch):
    request      [0, id, method, [args]]
    response     [1, id, error, result]
    notification [2, method, [args]]
To receive a notification, first claim its name with "$/register". The router
sends each name to one program only, so stop the BLE server while this runs.
"""

import socket
import sys

import msgpack

SOCKET_PATH = "/var/run/arduino-router.sock"
NAMES = ("pressed", "pair_hold")
REQUEST, RESPONSE, NOTIFY = 0, 1, 2


def main():
    sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    try:
        sock.connect(SOCKET_PATH)
    except OSError as e:
        sys.exit(f"Can't reach arduino-router at {SOCKET_PATH}: {e}")

    # Claim both names. Replies are checked in the loop below.
    for msg_id, name in enumerate(NAMES, start=1):
        sock.sendall(msgpack.packb([REQUEST, msg_id, "$/register", [name]]))

    unpacker = msgpack.Unpacker(raw=False)
    print("Listening. Press the button, or type p / h in the serial monitor.")
    while True:
        data = sock.recv(4096)
        if not data:
            sys.exit("Router closed the connection.")
        unpacker.feed(data)
        for msg in unpacker:
            if msg[0] == RESPONSE:
                _, msg_id, error, _result = msg
                name = NAMES[msg_id - 1]
                if error:
                    # e.g. the BLE server is running and already owns the name
                    print(f"Couldn't register {name!r}: {error}")
                else:
                    print(f"Registered {name!r}")
            elif msg[0] == NOTIFY:
                _, method, params = msg
                print(method, *params, flush=True)  # e.g. "pressed 1", "pair_hold"
            else:
                print(f"Other message: {msg!r}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
