// Atlist checklist device: MCU sketch (Arduino UNO Q).
// Drives the LCD, beeper and button. Linux tells it what to show over Bridge
// (see docs/Protocol.md, "Bridge" section).
//
// No button on hand yet, so the App Lab serial monitor stands in:
//   - every screen update is also printed to Monitor
//   - type "p" + Enter in the serial monitor to simulate a short press
//   - type "h" + Enter to simulate a 3-second hold (pair_hold)
//   - type "t" + Enter to run the 16-character cut test
//   - type "r" + Enter to show the resting screen
// Set HAS_LCD / HAS_BUTTON to true once the hardware is wired.

#include <Arduino_RouterBridge.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

const bool HAS_LCD    = true;   // 16x2 I2C LCD at 0x27
const bool HAS_BUTTON = false;  // button on pin 5, HIGH when pressed

LiquidCrystal_I2C lcd(0x27, 16, 2);

const int BUTTON_PIN  = 5;
const int SPEAKER_PIN = 8;
const int BEEP_HZ     = 1200;
const int BEEP_MS     = 150;
const unsigned long DEBOUNCE_MS = 25;
const unsigned long PAIR_HOLD_MS = 3000;  // hold this long to enter pair mode
const unsigned int  LCD_COLS    = 16;

// Button debounce state
int stableState = LOW;
int lastReading = LOW;
unsigned long lastChange = 0;

// Short press vs 3-second hold
unsigned long pressStart = 0;  // when the current press began
bool holdSent = false;         // pair_hold already sent for this press

// Total presses since boot, sent to Linux as "pressed"
unsigned int pressCount = 0;

// Resting screen text (Linux sets it with "rest")
String restLine1 = "Atlist";
String restLine2 = "Ready";

// Message state. A message stays on screen until something calls showRest().
bool messageShowing = false;
int  messageHold    = 0;  // 0 = until pressed, 1 = 1 hour, 2 = next reminder (used in step 5)

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

  Monitor.println("[LCD] " + a);
  Monitor.println("[LCD] " + b);
}

void beep() {
  tone(SPEAKER_PIN, BEEP_HZ, BEEP_MS);  // non-blocking: tone() stops itself
  Monitor.println("*beep*");
}

// Shows the resting screen. Whatever message was up is now gone.
void showRest() {
  showLines(restLine1, restLine2);
  messageShowing = false;
}

// --- Bridge: Linux -> sketch (docs/Protocol.md, Bridge table) --------------
// Registered with provide_safe, so these run between loop() passes, never in
// the middle of an LCD write. Parameter names avoid "beep" so they don't hide
// the beep() function.

// Bridge "show": show a message, beep if asked, remember hold for step 5.
bool showMessage(String line1, String line2, int doBeep, int hold) {
  showLines(line1, line2);
  if (doBeep == 1) {
    beep();
  }
  messageShowing = true;
  messageHold    = hold;
  return true;
}

// Bridge "rest": save new resting text; show it now only if no message is up.
bool setRest(String line1, String line2) {
  restLine1 = line1;
  restLine2 = line2;
  if (!messageShowing) {
    showRest();
  }
  return true;
}

// --- Sketch -> Linux (docs/Protocol.md, Bridge notifications) ------------

// A short press (real or typed): count it and tell Linux.
void onPress() {
  pressCount++;
  beep();
  showLines(restLine1, "Presses: " + String(pressCount));
  Bridge.notify("pressed", pressCount);
}

// The button was held 3 seconds (real or typed): ask Linux for pair mode.
// Not counted as a press. The screen changes when Linux calls pair_show.
void onPairHold() {
  Monitor.println("[pair_hold]");
  Bridge.notify("pair_hold");
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
    if (stableState == HIGH) {
      // Pressed: start timing. Don't decide yet, it may become a hold.
      pressStart = millis();
      holdSent   = false;
    } else if (!holdSent) {
      // Released before 3 s: a short press.
      onPress();
    }
  }

  // Still held after 3 s: send pair_hold once. The release then does nothing.
  if (stableState == HIGH && !holdSent && millis() - pressStart >= PAIR_HOLD_MS) {
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

  pinMode(BUTTON_PIN, INPUT);
  pinMode(SPEAKER_PIN, OUTPUT);

  if (HAS_LCD) {
    lcd.init();
    lcd.backlight();
  }

  showRest();

  if (!Bridge.provide_safe("show", showMessage)) {
    Monitor.println("ERROR: could not provide show");
  }
  if (!Bridge.provide_safe("rest", setRest)) {
    Monitor.println("ERROR: could not provide rest");
  }
}

void loop() {
  if (HAS_BUTTON) {
    readButton();
  }
  readMonitor();
}
