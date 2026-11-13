"""Step 1 test: call the sketch's "ping" over Bridge and print the reply.

Run on the UNO Q's Linux side, after flashing device/ping:
    ~/atlist-venv/bin/python device/ping_test.py

Talks MessagePack-RPC straight to arduino-router's socket (no App Lab):
    request  [0, id, method, [args]]
    response [1, id, error, result]
"""

import socket
import sys

import msgpack

SOCKET_PATH = "/var/run/arduino-router.sock"
TIMEOUT_S = 5


def call(method, *args):
    # 1. Reach the router. If this fails, the problem is on the Linux side.
    sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    sock.settimeout(TIMEOUT_S)
    try:
        sock.connect(SOCKET_PATH)
    except OSError as e:
        sys.exit(f"Can't reach arduino-router at {SOCKET_PATH}: {e}\n"
                 "Check: systemctl status arduino-router")

    # 2. Send the request and wait for the matching response.
    msg_id = 1
    sock.sendall(msgpack.packb([0, msg_id, method, list(args)]))
    unpacker = msgpack.Unpacker(raw=False)
    try:
        while True:
            data = sock.recv(4096)
            if not data:
                sys.exit("Router closed the connection without replying.")
            unpacker.feed(data)
            for msg in unpacker:
                if msg[0] == 1 and msg[1] == msg_id:
                    return msg[2], msg[3]  # (error, result)
    except socket.timeout:
        sys.exit(f"No reply after {TIMEOUT_S}s: is the ping sketch flashed and running?")
    finally:
        sock.close()


if __name__ == "__main__":
    error, result = call("ping")
    if error:
        # The router or sketch answered, but with an error (e.g. "ping" isn't provided).
        sys.exit(f"Sketch/router returned an error: {error}")
    print(f"Reply: {result!r}")
    print("OK: round trip works" if result == "pong" else "Unexpected reply")
