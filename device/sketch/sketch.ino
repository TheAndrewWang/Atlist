// Atlist checklist device: MCU sketch (Arduino UNO Q).
// Drives the LCD, beeper and button. Linux tells it what to show over Bridge
// (see docs/Protocol.md, "Bridge" section).
//
// No button or speaker on hand yet, so the App Lab serial monitor stands in:
//   - every screen update is also printed to Monitor, and beeps as *beep*
//   - type "p" + Enter in the serial monitor to simulate a short press
//   - type "h" + Enter to simulate a 3-second hold (pair_hold)
//   - type "t" + Enter to run the 16-character cut test
//   - type "r" + Enter to show the resting screen
// Set HAS_LCD / HAS_BUTTON to true once the hardware is wired.

#include <Arduino_RouterBridge.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

const bool HAS_LCD    = true;   // 16x2 I2C LCD at 0x27
const bool HAS_BUTTON = true;   // button from pin 5 to GND, INPUT_PULLUP: LOW when pressed

LiquidCrystal_I2C lcd(0x27, 16, 2);

const int BUTTON_PIN  = 5;
const int SPEAKER_PIN = 8;
const int BEEP_HZ     = 1200;
const int BEEP_MS     = 150;
const unsigned long DEBOUNCE_MS  = 25;
const unsigned long PAIR_HOLD_MS = 3000;     // hold this long to enter pair mode
const unsigned long HOLD_1_MS    = 3600000;  // hold 1: message times out after 60 min
const unsigned int  LCD_COLS     = 16;

// Check mark for the done state, stored in the LCD's custom character slot 1.
// (Slot 0 would be character 0, which ends a String early.)
const uint8_t CHECK_SLOT = 1;
const char    CHECK_CHAR = '\x01';
uint8_t checkBits[8] = {
  0b00000,
  0b00001,
  0b00011,
  0b10110,
  0b11100,
  0b01000,
  0b00000,
  0b00000,
};

// Button debounce state
int stableState = HIGH;  // HIGH = released (pull-up)
int lastReading = HIGH;
unsigned long lastChange = 0;

// Short press vs 3-second hold
unsigned long pressStart = 0;  // when the current press began
bool holdSent = false;         // pair_hold already sent for this press

// Total presses since boot, sent to Linux as "pressed"
unsigned int pressCount = 0;

// --- State -----------------------------------------------------------------
// The device is always in exactly one state; events (show, press, timeout,
// rest, clear_done, pair_show, pair_end) move it between them and then
// redraw the screen.
enum State { RESTING, SHOWING_MESSAGE, PAIR_MODE };
State state = RESTING;

// Pair mode: the passkey on screen, and where pair_end goes back to.
String passkey = "";
State stateBeforePair = RESTING;

// Resting screen text (Linux sets it with "rest")
String restLine1 = "Atlist";
String restLine2 = "Ready";

// The current message (Linux sets it with "show")
String msgLine1 = "";
String msgLine2 = "";
int msgHold = 0;                 // 0 = until pressed, 1 = pressed or 60 min, 2 = until next show
unsigned long msgShownAt = 0;    // millis() when the message went up, for hold 1

// Task checked off this reset cycle. Set by any short press, cleared by clear_done.
// While done, the resting screen says so until clear_done (the next reset
// cycle). Further presses still beep and send "pressed". A show never clears
// it: one device is one task, so a message doesn't start a new task.
bool done = false;
const char* DONE_LINE2 = "Already done";

// ---------------------------------------------------------------------------

// Clears the screen and shows two lines, each cut to 16 characters.
void showLines(const String& l1, const String& l2) {
  String a = l1.substring(0, LCD_COLS);
  String b = l2.substring(0, LCD_COLS);

  if (HAS_LCD) {
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print(a);
    lcd.setCursor(0, 1);
    lcd.print(b);
  }

  // The serial monitor can't show the LCD's custom character, so print a "✓".
  String ma = a;
  String mb = b;
  ma.replace(String(CHECK_CHAR), "✓");
  mb.replace(String(CHECK_CHAR), "✓");
  Monitor.println("[LCD] " + ma);
  Monitor.println("[LCD] " + mb);
}

void beep() {
  tone(SPEAKER_PIN, BEEP_HZ, BEEP_MS);  // non-blocking: tone() stops itself
  Monitor.println("*beep*");
}

// Draws whatever the current state says should be on screen.
// The done mark only goes on the resting screen ("✓ TAKE MEDS" / "Already
// done"), never on a message: a message hasn't been checked off.
void drawScreen() {
  if (state == PAIR_MODE) {
    showLines("PAIR MODE", passkey);
  } else if (state == SHOWING_MESSAGE) {
    showLines(msgLine1, msgLine2);
  } else if (done) {
    showLines(String(CHECK_CHAR) + " " + restLine1, DONE_LINE2);
  } else {
    showLines(restLine1, restLine2);
  }
}

// Goes back to the resting screen. Whatever message was up is now gone.
void showRest() {
  state = RESTING;
  drawScreen();
}

// --- Bridge: Linux -> sketch (docs/Protocol.md, Bridge table) --------------
// Registered with provide_safe, so these run between loop() passes, never in
// the middle of an LCD write. Parameter names avoid "beep" so they don't hide
// the beep() function.

// Bridge "show": show a message, beep if asked, keep it per hold.
// A new show replaces any message, whatever its hold. In pair mode the
// message is kept and appears after pair_end.
bool showMessage(String line1, String line2, int doBeep, int hold) {
  msgLine1   = line1;
  msgLine2   = line2;
  msgHold    = hold;
  msgShownAt = millis();
  if (state == PAIR_MODE) {
    stateBeforePair = SHOWING_MESSAGE;
  } else {
    state = SHOWING_MESSAGE;
  }
  drawScreen();
  if (doBeep == 1) {
    beep();
  }
  return true;
}

// Bridge "rest": save new resting text; show it now only if no message is up.
bool setRest(String line1, String line2) {
  restLine1 = line1;
  restLine2 = line2;
  if (state == RESTING) {
    drawScreen();
  }
  return true;
}

// Bridge "clear_done": a reset cycle ended, so the task is not done any more.
bool clearDone() {
  done = false;
  drawScreen();
  return true;
}

// Bridge "pair_show": show PAIR MODE and the passkey. Called again with a new
// passkey, it only updates the passkey (pair_end still goes back correctly).
bool pairShow(String key) {
  if (state != PAIR_MODE) {
    stateBeforePair = state;
    state = PAIR_MODE;
  }
  passkey = key;
  drawScreen();
  return true;
}

// Bridge "pair_end": back to the message or resting screen from before.
bool pairEnd() {
  if (state == PAIR_MODE) {
    state = stateBeforePair;
  }
  drawScreen();
  return true;
}

// --- Sketch -> Linux (docs/Protocol.md, Bridge notifications) ------------

// A short press (real or typed): check off the task, tell Linux, and leave
// the message unless its hold says to keep it (hold 2). Ignored in pair mode.
void onPress() {
  if (state == PAIR_MODE) {
    Monitor.println("[press ignored: pair mode]");
    return;
  }
  pressCount++;
  beep();
  done = true;
  Bridge.notify("pressed", pressCount);
  Monitor.println("[pressed " + String(pressCount) + "]");

  if (state == SHOWING_MESSAGE && msgHold != 2) {
    state = RESTING;
  }
  drawScreen();
}

// The button was held 3 seconds (real or typed): ask Linux for pair mode.
// Not counted as a press. The screen changes when Linux calls pair_show.
void onPairHold() {
  Monitor.println("[pair_hold]");
  Bridge.notify("pair_hold");
}

// hold 1: the message goes away by itself after 60 minutes.
void checkHoldTimeout() {
  if (state == SHOWING_MESSAGE && msgHold == 1 && millis() - msgShownAt >= HOLD_1_MS) {
    showRest();
  }
}

// ---------------------------------------------------------------------------

void readButton() {
  int reading = digitalRead(BUTTON_PIN);

  if (reading != lastReading) {
    lastChange  = millis();
    lastReading = reading;
  }

  if (millis() - lastChange > DEBOUNCE_MS && reading != stableState) {
    stableState = reading;
    if (stableState == LOW) {
      // Pressed: start timing. Don't decide yet, it may become a hold.
      pressStart = millis();
      holdSent   = false;
    } else if (!holdSent) {
      // Released before 3 s: a short press.
      onPress();
    }
  }

  // Still held after 3 s: send pair_hold once. The release then does nothing.
  if (stableState == LOW && !holdSent && millis() - pressStart >= PAIR_HOLD_MS) {
    holdSent = true;
    onPairHold();
  }
}

// Serial monitor commands, standing in for missing hardware.
void readMonitor() {
  if (!Monitor.available()) return;

  String cmd = Monitor.readStringUntil('\n');
  cmd.trim();

  if (cmd == "p") {
    onPress();
  } else if (cmd == "h") {
    onPairHold();
  } else if (cmd == "t") {
    showLines("12345678901234567890", "x");  // expect only 1234567890123456
  } else if (cmd == "r") {
    showRest();
  } else if (cmd.length() > 0) {
    Monitor.println("? commands: p = press, h = 3 s hold, t = cut test, r = rest screen");
  }
}

// ---------------------------------------------------------------------------

void setup() {
  Bridge.begin();
  Monitor.begin(115200);

  pinMode(BUTTON_PIN, INPUT_PULLUP);  // internal pull-up: no resistor needed
  pinMode(SPEAKER_PIN, OUTPUT);

  if (HAS_LCD) {
    lcd.init();
    lcd.backlight();
    lcd.createChar(CHECK_SLOT, checkBits);
  }

  showRest();

  if (!Bridge.provide_safe("show", showMessage)) {
    Monitor.println("ERROR: could not provide show");
  }
  if (!Bridge.provide_safe("rest", setRest)) {
    Monitor.println("ERROR: could not provide rest");
  }
  if (!Bridge.provide_safe("clear_done", clearDone)) {
    Monitor.println("ERROR: could not provide clear_done");
  }
  if (!Bridge.provide_safe("pair_show", pairShow)) {
    Monitor.println("ERROR: could not provide pair_show");
  }
  if (!Bridge.provide_safe("pair_end", pairEnd)) {
    Monitor.println("ERROR: could not provide pair_end");
  }
}

void loop() {
  if (HAS_BUTTON) {
    readButton();
  }
  readMonitor();
  checkHoldTimeout();
}
