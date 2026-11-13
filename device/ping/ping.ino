// Step 1 test sketch: proves Linux can call a function on the MCU over Bridge.
// Linux calls "ping" and should get "pong" back. Test with device/ping_test.py.

#include <Arduino_RouterBridge.h>

// The function Linux calls. Its return value is sent back as the reply.
String ping() {
  return "pong";
}

void setup() {
  Bridge.begin();                     // connect to arduino-router on the Linux side
  Bridge.provide_safe("ping", ping);  // answer calls named "ping"; runs between loop() passes
}

void loop() {
  // Nothing to do. Bridge handles "ping" calls after each loop() pass.
}
