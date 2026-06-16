# Beacon
Bluetooth Low Energy Utility - Windows Application

<img width="1915" height="1039" alt="image" src="https://github.com/user-attachments/assets/2ffa0f8e-1a63-46b6-aae4-2980bf23a539" />
An open-source Windows Forms (.NET Framework **4.8.1**) engineering tool for **sniffing, reading,
writing and emulating Bluetooth Low Energy** devices. It uses the native Windows 10/11 Bluetooth
stack (WinRT) for the built-in/USB radio, and an **HCI-over-serial (H4)** transport for external
adapters and sniffers.

**Open source project by [Jakka351](https://github.com/jakka351) — https://github.com/jakka351/beacon**

![status: builds & runs](https://img.shields.io/badge/build-passing-brightgreen)

---

## Features

| Area | What it does |
|------|--------------|
| **Scanner** | Continuous LE advertisement scan (active/passive). Per-device aggregation showing **MAC address, address kind** (public / static / resolvable-private), **RSSI, Tx power, advertising type** (ADV_IND, SCAN_RSP, …) and **adv count**. Live filter box. |
| **Advertisement decode** | Full breakdown of every AD structure (type, name, interpreted value, raw hex): Flags, Complete/Shortened Local Name, 16/128-bit Service UUIDs, Service Data, Manufacturer Data (with company-ID lookup), Tx Power, Appearance, URI, … |
| **GATT Explorer** | Connect to a peripheral, enumerate the full **service / characteristic / descriptor** tree, see each characteristic's properties, and **Read / Write / Write-without-response / Subscribe (Notify & Indicate)**. Notifications stream into a value-history grid. Flexible write input (text, `01 A2 FF`, `0x41,0x42`, decimal CSV). |
| **Packet Sniffer** | Every advertisement, GATT op and HCI frame is recorded to a unified, virtualised packet timeline (index, time, source, direction, type, address, RSSI, length, summary) with a synchronized **hex viewer**. Source/text filters, pause and auto-scroll. |
| **Device Emulator** | **Peripheral mode** — publish a connectable **GATT server** (`GattServiceProvider`) with user-defined service + characteristics, live read/write handling and **push notifications**. **Broadcast mode** — advertise a manufacturer-data / service payload (`BluetoothLEAdvertisementPublisher`). |
| **External adapters (Serial/USB)** | Talk to an external controller or sniffer exposing the **HCI UART (H4)** protocol over a COM port (incl. USB-CDC). Robust H4 framer + HCI decoder (LE Advertising Reports, Extended Adv, events). Issue HCI Reset / LE-scan commands to drive the controller. COM ports and USB Bluetooth radios are enumerated via WMI. |
| **Fuzzer** | Write-fuzz a selected characteristic (zeros / ones / increment / walking-bit / random), configurable length range, rate and packet cap. |
| **Export** | **CSV** and **text** for all packets; **BTSnoop** (Wireshark-readable) for advertising + HCI traffic. |
| **Console** | Colour-coded activity log, also written to `bin\…\logs\session-*.log`. |

The UI is the standard WinForms theme — menu / toolbar / status bar, a device grid on the left and
a tabbed workspace on the right — and starts **maximized**. Press **F11** for true full-screen.

---

## Requirements

- **Windows 10 or 11** with a Bluetooth 4.0+ (LE-capable) radio.
- **.NET Framework 4.8.1** runtime (developer machine needs the 4.8.1 targeting pack).
- Visual Studio 2022 (or Build Tools) for building. NuGet restore needs internet the first time
  (pulls `Microsoft.Windows.SDK.Contracts`, which provides the WinRT projections).

## Build

```powershell
# From the repo root
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" BleWorkbench.sln -t:Restore
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" BleWorkbench.sln -p:Configuration=Release
```

Or just open `BleWorkbench.sln` in Visual Studio 2022 and press **F5**.

Output: `BleWorkbench\bin\Release\Beacon.exe`.

> **WinRT in .NET Framework:** the project references `Microsoft.Windows.SDK.Contracts` via
> `PackageReference`, which supplies both the `Windows.*` projections and the
> `System.Runtime.WindowsRuntime` facade needed to `await` `IAsyncOperation`. The UI is built in
> code (no designer `.resx`) so the partial `MainForm.*.cs` files compile cleanly outside the VS
> designer.

## Run / quick start

1. Launch the app. The status bar confirms the radio: *“Bluetooth LE radio is ON and ready …”*.
2. **Start Scan** (toolbar or `F5`). Devices stream into the left grid; select one to see its
   advertisement breakdown.
3. **Connect** (toolbar / double-click a connectable device) → switch to **GATT Explorer** to read,
   write and subscribe.
4. Watch raw traffic on **Packet Sniffer**; export it from **File ▸ Export**.
5. Try **Emulator** to publish a peripheral or broadcast, and **Fuzzer** to stress a writable
   characteristic.

### Using an external HCI adapter / sniffer
Pick the COM port + baud in the toolbar → **Open HCI**. Use **Tools ▸ Start LE scan on serial
adapter** to send `HCI_Reset` + `LE_Set_Scan_Enable`; decoded advertising reports populate the same
device list and packet log. Source-filter the sniffer to `HCI` to see raw frames.

---

## Important platform notes & limitations

- **One Bluetooth radio at a time.** Windows binds a single BT radio. If you have a built-in radio
  **and** a USB dongle, the second one typically fails with **Device Manager Code 31 /
  `CM_PROB_FAILED_ADD`**, and a failed radio in the stack makes *all* BLE software see *no* radio.
  Disable the one you don't want (Device Manager) so the other can bind. See the troubleshooting
  script below.
- **Advertisement publisher (Broadcast mode):** Windows reserves the GAP fields (Flags, Local Name,
  Service UUIDs) of a publisher payload; on many builds only **manufacturer/data sections** are
  broadcast. The app tries the full payload and automatically falls back to manufacturer-data-only.
  To advertise a name + service UUID, use **Peripheral mode** (`GattServiceProvider`).
- **Local adapter MAC** cannot be changed from a desktop app on Windows — use an external HCI
  adapter for full control of address/advertising fields.
- **BTSnoop export** covers advertising + HCI frames (advertising reports are synthesised as HCI LE
  Advertising Report events). GATT client/server operations are not represented in BTSnoop; use CSV
  for those.

---

## Bluetooth troubleshooting helper

If scanning shows nothing, the culprit is almost always the radio/driver, not the app. Helper
scripts used during bring-up are included:

- `fixbt.ps1` – restart Bluetooth service, disable→enable + remove→rescan a faulted dongle.
- `fixbt2.ps1` – make a USB dongle the **sole** radio (disables the built-in, re-adds the dongle).
- `probe/` – a tiny standalone console app that reports radio state and runs a 12-second
  advertisement scan, independent of the main app, to isolate hardware/driver issues from software.

Run the elevated ones from an **Administrator** PowerShell. After disabling/swapping radios, a
**replug of the USB dongle** (or a reboot) re-initialises the radio cleanly.

```powershell
# Quick independent radio + scan check:
probe\bin\probe.exe
```

---

## Project layout

```
BleWorkbench.sln
BleWorkbench/
  Program.cs, App.config, Properties/AssemblyInfo.cs
  Core/        HexUtil, BleAddress, AssignedNumbers, AdvertisementParser,
               DeviceRegistry, PacketLog, AppLog, CaptureExport
  Models/      BleDeviceInfo, AdvertisementSection, BlePacket, EmulationModels, enums
  Ble/         BleScanner, GattClient, PeripheralServer, BleRadio, GattModels, BleBuffers
  Transport/   HciFramer, HciDecoder, SerialHciTransport, PortEnumerator
  Controls/    HexView
  Forms/       MainForm (+ .Devices/.Gatt/.Sniffer/.Emulator/.Fuzzer/.Console partials)
ble.py         Original Python/bleak prototype (reference only)
```
