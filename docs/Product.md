# **Atlist Hardware**

# **What the device is**

**Atlist is a set of small devices, each about the size of a clock radio, that stand on a table or hang on a wall. Each device stands for one daily task, such as taking medication, brushing teeth, drinking water, or taking keys and wallet.**

**Each device sits where its task happens. For example, the medication device sits next to the medicine cabinet. It shows the task on a small screen. The person presses one large button to check the task off. A device that shows "done" tells the person, at a glance, that they already did that task. After a set amount of time the device resets to "not done" for the next cycle.**

**The person using it never logs in, never sees a menu and never types. Atlist is a reminder, not a medical tracker.**

## **Design rules**

**Every hardware and CAD decision is checked against these.**

| Rule | What it means for hardware |
| ----- | ----- |
| **As simple as a light switch** | **One main control per device. No modes the user can reach.** |
| **One task per device** | **Each unit has one fixed task. The task is shown on the screen and on a swappable label.** |
| **Large and tactile** | **The main button is big, high-contrast and gives a clear click.** |
| **No sense of surveillance** | **No microphone and no camera on the device, ever.** |
| **Done is obvious** | **Done vs not done must be easy to read. A light that can be seen from across the room is a future option.** |
| **Works where the task is** | **Must stand on a counter or hang on a wall, and survive a bathroom or kitchen.** |
| **Setup is hidden** | **Any setup or debug buttons are recessed, on the back or bottom, or need a tool to reach.** |

## **Designing for people living with dementia**

* **No accidental presses: the button needs a deliberate press. A brush with a sleeve should not trigger it.**  
* **No half states: from the user's point of view the device is either done or not done. Nothing in between.**  
* **No hidden modes: the user must not be able to reach anything that changes how the device behaves.**  
* **Built to be handled roughly: expect it to be picked up, knocked over and pressed hard. Design for a drop from table height.**  
* **Safe to touch: no sharp edges, no small parts that can come off (choking hazard), no exposed wires, nothing hot.**

## **Board**

**The device runs on an Arduino UNO Q. It has two processors on one board: a Linux processor (MPU) and a microcontroller (MCU).**

**A shield plugs into the UNO headers on top of the board. The screen, speaker and button are all soldered to that shield, so they connect to the MCU through the shield. The whole electronics package is one two-layer stack: UNO Q on the bottom, shield on top.**

| Item | Value |
| ----- | ----- |
| **Board size** | **68.58 x 53.34 mm (same outline as a classic UNO)** |
| **Power in** | **USB-C, 5 V up to 3 A. The VIN pin also accepts 7 to 24 V.** |
| **MCU logic level** | **3.3 V. Most MCU pins are 5 V tolerant. A0 and A1 are not.** |
| **Wireless** | **Wi-Fi and Bluetooth 5.1, onboard antenna** |
|  |  |

## **Parts list**

| Part | Qty |  |  |
| ----- | ----- | ----- | ----- |
| **Arduino UNO Q** | **1** |  |  |
| **Shield** | **1** |  |  |
| **16x2 character screen, soldered to the shield** | **1** |  |  |
| **Speaker, soldered to the shield** | **1** |  |  |
| **Button, soldered to the shield** | **1** |  |  |
| **Resistor for the speaker** | **1** |  |  |
| **USB-C power supply, 5 V 3 A** | **1** |  |  |
| **3D printed enclosure** | **1**  |  |  |
|  |  |  |  |
|  |  |  |  |

## **Shield**

**The shield is the top layer of the stack. Everything the user sees, hears or touches is soldered or wired to it.**

**We use a shield because the Arduino UNO Q has only a few 5V and GND pins, and the screen, speaker and button all need power and ground. The shield connects to the board’s 5V and GND pins and gives us more connection points for them, so each part can be wired to power and ground directly.**

## **Screen**

* **16x2 character screen. Each row fits 16 characters.**  
* **Shows the task name and whether the task is done or not done. This is the main way the user knows they have already done the task, so it has to be easy to read at a glance.**  
* **Sits in a slot in the enclosure lid, facing the user. The slot shows the full screen so no text is cut off at the edges.**  
* **Sits slightly below the lid surface so it is protected from scratches and presses.**  
* **Angled toward the user so it can be read while standing.**  
* **Backlit, so it can be read in a dim room.**  
* **Wired to the shield.**

