// Atlist checklist device: MCU sketch (Arduino UNO Q).
// Drives the LCD, beeper and button. Linux tells it what to show over Bridge
// (see docs/Protocol.md, "Bridge" section).
//
// No LCD or button on hand yet, so the App Lab serial monitor stands in:
//   - every screen update is also printed to Monitor
//   - type "p" + Enter in the serial monitor to simulate a button press
//   - type "t" + Enter to run the 16-character cut test
// Set HAS_LCD / HAS_BUTTON to true once the hardware is wired.

#include <Arduino_RouterBridge.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

const bool HAS_LCD    = false;  // 16x2 I2C LCD at 0x27
const bool HAS_BUTTON = false;  // button on pin 5, HIGH when pressed

LiquidCrystal_I2C lcd(0x27, 16, 2);

const int BUTTON_PIN  = 5;
const int SPEAKER_PIN = 8;
const int BEEP_HZ     = 1200;
const int BEEP_MS     = 150;
const unsigned long DEBOUNCE_MS = 25;
const unsigned int  LCD_COLS    = 16;

// Button debounce state
int stableState = LOW;
int lastReading = LOW;
unsigned long lastChange = 0;

// Total presses since boot (becomes the "pressed" count in step 4)
unsigned int pressCount = 0;

// Resting screen text (Linux sets it with "rest" in step 3)
String restLine1 = "Atlist";
String restLine2 = "Ready";

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

void showRest() {
  showLines(restLine1, restLine2);
}

// A debounced button press (real or typed).
void onPress() {
  pressCount++;
  beep();
  showLines(restLine1, "Presses: " + String(pressCount));
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
      onPress();
    }
  }
}

// Serial monitor commands, standing in for missing hardware.
void readMonitor() {
  if (!Monitor.available()) return;

  String cmd = Monitor.readStringUntil('\n');
  cmd.trim();

  if (cmd == "p") {
    onPress();
  } else if (cmd == "t") {
    showLines("12345678901234567890", "x");  // expect only 1234567890123456
  } else if (cmd == "r") {
    showRest();
  } else if (cmd.length() > 0) {
    Monitor.println("? commands: p = press, t = cut test, r = rest screen");
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
}

void loop() {
  if (HAS_BUTTON) {
    readButton();
  }
  readMonitor();
}
