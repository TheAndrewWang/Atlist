// LCD wiring test: checks the 16x2 I2C LCD on its own, without the real sketch.
//
// Wiring (UNO Q, main I2C bus "Wire"):
//   LCD GND -> GND      LCD VCC -> 5V
//   LCD SDA -> SDA pin (second-to-last on the digital header, past AREF; D20)
//   LCD SCL -> SCL pin (last pin on the digital header; D21)
//
// What it does:
//   1. Scans the I2C bus every 3 s and prints every address it finds to the
//      App Lab serial monitor. An LCD backpack is usually 0x27 or 0x3F.
//   2. The first time it finds one of those, starts the LCD at that address and
//      shows "LCD test OK" / the address, then counts seconds on line 2.
//
// Reading the result:
//   "found 0x27" + text on the LCD   -> wiring and address are fine.
//   "found 0x27" but no text         -> turn the contrast pot on the backpack.
//   "no I2C devices found"           -> wiring: check SDA/SCL pins, 5V and GND.

#include <Arduino_RouterBridge.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

LiquidCrystal_I2C* lcd = nullptr;  // created once we know the address
uint8_t lcdAddr = 0;

unsigned long lastScan = 0;
unsigned long lastTick = 0;
unsigned long seconds  = 0;

// Returns true if a device answers at this address.
bool present(uint8_t addr) {
  Wire.beginTransmission(addr);
  return Wire.endTransmission() == 0;
}

void printHex(uint8_t addr) {
  Monitor.print("0x");
  if (addr < 16) Monitor.print("0");
  Monitor.print(addr, HEX);
}

void scan() {
  Monitor.println("Scanning I2C bus (Wire: SDA/SCL pins)...");
  int found = 0;
  for (uint8_t addr = 1; addr < 127; addr++) {
    if (present(addr)) {
      Monitor.print("  found ");
      printHex(addr);
      Monitor.println();
      found++;
    }
  }
  if (found == 0) {
    Monitor.println("  no I2C devices found: check SDA/SCL, 5V, GND");
  }
}

void startLcd(uint8_t addr) {
  lcdAddr = addr;
  lcd = new LiquidCrystal_I2C(addr, 16, 2);
  lcd->init();
  lcd->backlight();
  lcd->clear();
  lcd->setCursor(0, 0);
  lcd->print("LCD test OK");
  lcd->setCursor(0, 1);
  lcd->print("addr 0x");
  lcd->print(addr, HEX);

  Monitor.print("LCD started at ");
  printHex(addr);
  Monitor.println(": it should say \"LCD test OK\"");
}

void setup() {
  Bridge.begin();
  Monitor.begin();
  Wire.begin();
}

void loop() {
  unsigned long now = millis();

  // Scan every 3 s until the LCD is running, then every 10 s.
  unsigned long every = lcd ? 10000 : 3000;
  if (now - lastScan >= every) {
    lastScan = now;
    scan();
    if (!lcd) {
      if (present(0x27))      startLcd(0x27);
      else if (present(0x3F)) startLcd(0x3F);
    }
  }

  // Once running, count seconds on line 2 so you can see it is alive.
  if (lcd && now - lastTick >= 1000) {
    lastTick = now;
    seconds++;
    lcd->setCursor(10, 1);
    lcd->print(seconds);
    lcd->print("s ");
  }
}
