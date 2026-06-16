using System;
using System.Threading.Tasks;
using BleWorkbench.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;

namespace BleWorkbench.Ble
{
    public class RadioHealth
    {
        public bool AdapterPresent;
        public bool LowEnergySupported;
        public bool CentralRoleSupported;
        public bool RadioUsable;
        public RadioState RadioState;
        public string Summary = string.Empty;
        public string Remediation = string.Empty;

        // The advertisement watcher can work even when the Radio-management API is
        // flaky (some USB dongles expose a usable HCI radio but no Radio object), so
        // only a missing adapter or missing LE support is a hard blocker.
        public bool CanScan { get { return AdapterPresent && LowEnergySupported && RadioState != RadioState.Disabled; } }
    }

    /// <summary>
    /// Probes the Windows Bluetooth radio so the UI can tell the user *why* a scan
    /// produces nothing (radio off, no adapter, adapter present but radio
    /// unavailable due to a driver/dongle conflict) instead of failing silently.
    /// </summary>
    public static class BleRadio
    {
        public static async Task<RadioHealth> CheckAsync()
        {
            var h = new RadioHealth();
            try
            {
                BluetoothAdapter adapter = await BluetoothAdapter.GetDefaultAsync();
                if (adapter == null)
                {
                    h.Summary = "No Bluetooth adapter is available to Windows.";
                    h.Remediation = "Plug in or enable a Bluetooth adapter, then click Refresh.";
                    return h;
                }

                h.AdapterPresent = true;
                h.LowEnergySupported = adapter.IsLowEnergySupported;
                h.CentralRoleSupported = adapter.IsCentralRoleSupported;

                if (!adapter.IsLowEnergySupported)
                {
                    h.Summary = "The default Bluetooth adapter does not support Bluetooth Low Energy.";
                    h.Remediation = "Use a BLE-capable (Bluetooth 4.0+) adapter.";
                    return h;
                }

                try
                {
                    Radio radio = await adapter.GetRadioAsync();
                    if (radio == null)
                    {
                        h.Summary = "The Bluetooth adapter is present but its radio object is unavailable.";
                        h.Remediation = "Turn Bluetooth on, or remove a conflicting/failed Bluetooth dongle.";
                        return h;
                    }
                    h.RadioState = radio.State;
                    h.RadioUsable = true;
                    if (radio.State == RadioState.On)
                    {
                        h.Summary = "Bluetooth LE radio is ON and ready (address " +
                                    adapter.BluetoothAddress.ToString("X12") + ").";
                    }
                    else
                    {
                        h.Summary = "Bluetooth radio is " + radio.State + ".";
                        h.Remediation = "Turn Bluetooth on (Settings > Bluetooth & devices, or the Action Center toggle).";
                    }
                }
                catch
                {
                    // Adapter is present and LE-capable, but the Radio-management object is
                    // unavailable (common on some USB dongles). Scanning typically still works.
                    h.RadioUsable = false;
                    h.Summary = "BLE adapter present (address " + adapter.BluetoothAddress.ToString("X12") +
                                ", LE + Central supported). Scanning enabled.";
                    h.Remediation = string.Empty;
                }
            }
            catch (Exception ex)
            {
                h.Summary = "Bluetooth radio check failed: " + ex.Message;
                h.Remediation = "Verify the Bluetooth Support Service is running and a radio is enabled.";
            }
            return h;
        }

        public static async Task LogAsync()
        {
            RadioHealth h = await CheckAsync();
            if (h.CanScan) AppLog.Success(h.Summary);
            else
            {
                AppLog.Warn(h.Summary);
                if (!string.IsNullOrEmpty(h.Remediation)) AppLog.Warn("Fix: " + h.Remediation);
            }
        }
    }
}
