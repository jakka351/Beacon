using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using BleWorkbench.Core;

namespace BleWorkbench.Transport
{
    public class SerialPortInfo
    {
        public string Port { get; set; }          // e.g. "COM3"
        public string Description { get; set; }    // friendly name
        public string DeviceId { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Description) ? Port : Port + " - " + Description;
        }
    }

    public class UsbDeviceInfo
    {
        public string Name { get; set; }
        public string DeviceId { get; set; }
        public string Service { get; set; }

        public override string ToString() { return Name; }
    }

    /// <summary>
    /// Enumerates serial / USB-CDC COM ports and USB Bluetooth radios so the user
    /// can pick an external adapter or sniffer. Uses WMI for friendly names with a
    /// plain <see cref="SerialPort.GetPortNames"/> fallback.
    /// </summary>
    public static class PortEnumerator
    {
        public static List<SerialPortInfo> ListSerialPorts()
        {
            var result = new List<SerialPortInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = (mo["Name"] as string) ?? string.Empty;
                        Match m = Regex.Match(name, @"\((COM\d+)\)");
                        if (!m.Success) continue;
                        string com = m.Groups[1].Value;
                        if (!seen.Add(com)) continue;
                        result.Add(new SerialPortInfo
                        {
                            Port = com,
                            Description = Regex.Replace(name, @"\s*\(COM\d+\)", string.Empty),
                            DeviceId = (mo["DeviceID"] as string) ?? string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("WMI port enumeration failed, using fallback: " + ex.Message);
            }

            // Fallback / fill in any ports WMI missed.
            try
            {
                foreach (string p in SerialPort.GetPortNames())
                {
                    string norm = p.Trim();
                    if (seen.Add(norm))
                        result.Add(new SerialPortInfo { Port = norm, Description = "Serial port" });
                }
            }
            catch { }

            result.Sort((a, b) => ComNumber(a.Port).CompareTo(ComNumber(b.Port)));
            return result;
        }

        public static List<UsbDeviceInfo> ListBluetoothRadios()
        {
            var result = new List<UsbDeviceInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, Service FROM Win32_PnPEntity " +
                    "WHERE PNPClass='Bluetooth' OR Service='BTHUSB' OR Service='BthLEEnum'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        result.Add(new UsbDeviceInfo
                        {
                            Name = (mo["Name"] as string) ?? "(unknown)",
                            DeviceId = (mo["DeviceID"] as string) ?? string.Empty,
                            Service = (mo["Service"] as string) ?? string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("USB Bluetooth enumeration failed: " + ex.Message);
            }
            return result;
        }

        private static int ComNumber(string com)
        {
            Match m = Regex.Match(com ?? string.Empty, @"\d+");
            int n;
            return m.Success && int.TryParse(m.Value, out n) ? n : int.MaxValue;
        }
    }
}
