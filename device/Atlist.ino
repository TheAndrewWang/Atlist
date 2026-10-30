#include <Wire.h>
#include <LiquidCrystal_I2C.h>

LiquidCrystal_I2C lcd(0x27, 16, 2);

const int BUTTON_PIN  = 5;
const int SPEAKER_PIN = 8;
const int BEEP_HZ     = 1200;
const int BEEP_MS     = 150;
const unsigned long DEBOUNCE_MS = 25;

int stableState = LOW;
int lastReading = LOW;
unsigned long lastChange = 0;
unsigned int pressCount  = 0;

void setup() {
  pinMode(BUTTON_PIN, INPUT);
  pinMode(SPEAKER_PIN, OUTPUT);
  lcd.init();
  lcd.backlight();
  showState();
}

void loop() {
  int reading = digitalRead(BUTTON_PIN);

  if (reading != lastReading) {
    lastChange  = millis();
    lastReading = reading;
  }

  if (millis() - lastChange > DEBOUNCE_MS && reading != stableState) {
    stableState = reading;
    if (stableState == HIGH) {
      pressCount++;
      tone(SPEAKER_PIN, BEEP_HZ, BEEP_MS);
    }
    showState();
  }
}

void showState() {
  lcd.setCursor(0, 0);
  lcd.print(stableState == HIGH ? "PRESSED " : "waiting ");

  lcd.setCursor(0, 1);
  lcd.print("Presses: ");
  lcd.print(pressCount);
  lcd.print("   ");
}