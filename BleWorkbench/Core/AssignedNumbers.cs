using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BleWorkbench.Core
{
    /// <summary>
    /// A practical subset of the Bluetooth SIG "Assigned Numbers": GATT service /
    /// characteristic / descriptor names, advertising data (AD) type names,
    /// company identifiers, plus helpers to recognise 16-bit UUIDs that use the
    /// Bluetooth Base UUID and to interpret common advertising sections.
    /// </summary>
    public static class AssignedNumbers
    {
        // 0000xxxx-0000-1000-8000-00805F9B34FB
        public static readonly Guid BluetoothBaseUuid =
            new Guid("00000000-0000-1000-8000-00805F9B34FB");

        private static readonly byte[] BaseTail =
        {
            0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5F, 0x9B, 0x34, 0xFB
        };

        #region tables
        private static readonly Dictionary<ushort, string> Services = new Dictionary<ushort, string>
        {
            { 0x1800, "Generic Access" },
            { 0x1801, "Generic Attribute" },
            { 0x1802, "Immediate Alert" },
            { 0x1803, "Link Loss" },
            { 0x1804, "Tx Power" },
            { 0x1805, "Current Time" },
            { 0x1806, "Reference Time Update" },
            { 0x1807, "Next DST Change" },
            { 0x1808, "Glucose" },
            { 0x1809, "Health Thermometer" },
            { 0x180A, "Device Information" },
            { 0x180D, "Heart Rate" },
            { 0x180E, "Phone Alert Status" },
            { 0x180F, "Battery" },
            { 0x1810, "Blood Pressure" },
            { 0x1811, "Alert Notification" },
            { 0x1812, "Human Interface Device" },
            { 0x1813, "Scan Parameters" },
            { 0x1814, "Running Speed and Cadence" },
            { 0x1815, "Automation IO" },
            { 0x1816, "Cycling Speed and Cadence" },
            { 0x1818, "Cycling Power" },
            { 0x1819, "Location and Navigation" },
            { 0x181A, "Environmental Sensing" },
            { 0x181B, "Body Composition" },
            { 0x181C, "User Data" },
            { 0x181D, "Weight Scale" },
            { 0x181E, "Bond Management" },
            { 0x181F, "Continuous Glucose Monitoring" },
            { 0x1820, "Internet Protocol Support" },
            { 0x1821, "Indoor Positioning" },
            { 0x1822, "Pulse Oximeter" },
            { 0x1823, "HTTP Proxy" },
            { 0x1824, "Transport Discovery" },
            { 0x1825, "Object Transfer" },
            { 0x1826, "Fitness Machine" },
            { 0x1827, "Mesh Provisioning" },
            { 0x1828, "Mesh Proxy" },
            { 0x183A, "Insulin Delivery" },
            { 0x1843, "Audio Input Control" },
            { 0x1844, "Volume Control" },
            { 0x1845, "Volume Offset Control" },
            { 0x184E, "Audio Stream Control" },
            { 0x184F, "Broadcast Audio Scan" },
            { 0xFE59, "Nordic DFU (Secure)" },
            { 0xFD6F, "Exposure Notification" },
        };

        private static readonly Dictionary<ushort, string> Characteristics = new Dictionary<ushort, string>
        {
            { 0x2A00, "Device Name" },
            { 0x2A01, "Appearance" },
            { 0x2A02, "Peripheral Privacy Flag" },
            { 0x2A03, "Reconnection Address" },
            { 0x2A04, "Peripheral Preferred Connection Parameters" },
            { 0x2A05, "Service Changed" },
            { 0x2A06, "Alert Level" },
            { 0x2A07, "Tx Power Level" },
            { 0x2A08, "Date Time" },
            { 0x2A19, "Battery Level" },
            { 0x2A1C, "Temperature Measurement" },
            { 0x2A1E, "Intermediate Temperature" },
            { 0x2A23, "System ID" },
            { 0x2A24, "Model Number String" },
            { 0x2A25, "Serial Number String" },
            { 0x2A26, "Firmware Revision String" },
            { 0x2A27, "Hardware Revision String" },
            { 0x2A28, "Software Revision String" },
            { 0x2A29, "Manufacturer Name String" },
            { 0x2A2B, "Current Time" },
            { 0x2A37, "Heart Rate Measurement" },
            { 0x2A38, "Body Sensor Location" },
            { 0x2A39, "Heart Rate Control Point" },
            { 0x2A3F, "Alert Status" },
            { 0x2A46, "New Alert" },
            { 0x2A4A, "HID Information" },
            { 0x2A4B, "Report Map" },
            { 0x2A4C, "HID Control Point" },
            { 0x2A4D, "Report" },
            { 0x2A4E, "Protocol Mode" },
            { 0x2A50, "PnP ID" },
            { 0x2A5B, "CSC Measurement" },
            { 0x2A63, "Cycling Power Measurement" },
            { 0x2A6D, "Pressure" },
            { 0x2A6E, "Temperature" },
            { 0x2A6F, "Humidity" },
            { 0x2AB3, "TDS Control Point" },
            { 0x2A9D, "Weight Measurement" },
            { 0x2A98, "Weight" },
        };

        private static readonly Dictionary<ushort, string> Descriptors = new Dictionary<ushort, string>
        {
            { 0x2900, "Characteristic Extended Properties" },
            { 0x2901, "Characteristic User Description" },
            { 0x2902, "Client Characteristic Configuration" },
            { 0x2903, "Server Characteristic Configuration" },
            { 0x2904, "Characteristic Presentation Format" },
            { 0x2905, "Characteristic Aggregate Format" },
            { 0x2906, "Valid Range" },
            { 0x2907, "External Report Reference" },
            { 0x2908, "Report Reference" },
            { 0x290B, "Environmental Sensing Configuration" },
            { 0x290C, "Environmental Sensing Measurement" },
        };

        // BLE advertising data types, Core Specification Supplement, Part A, 1.3.
        private static readonly Dictionary<byte, string> AdTypes = new Dictionary<byte, string>
        {
            { 0x01, "Flags" },
            { 0x02, "Incomplete 16-bit Service UUIDs" },
            { 0x03, "Complete 16-bit Service UUIDs" },
            { 0x04, "Incomplete 32-bit Service UUIDs" },
            { 0x05, "Complete 32-bit Service UUIDs" },
            { 0x06, "Incomplete 128-bit Service UUIDs" },
            { 0x07, "Complete 128-bit Service UUIDs" },
            { 0x08, "Shortened Local Name" },
            { 0x09, "Complete Local Name" },
            { 0x0A, "Tx Power Level" },
            { 0x0D, "Class of Device" },
            { 0x10, "Device ID / Security Manager TK" },
            { 0x11, "Security Manager Out-of-Band Flags" },
            { 0x12, "Peripheral Connection Interval Range" },
            { 0x14, "16-bit Service Solicitation UUIDs" },
            { 0x15, "128-bit Service Solicitation UUIDs" },
            { 0x16, "16-bit Service Data" },
            { 0x17, "Public Target Address" },
            { 0x18, "Random Target Address" },
            { 0x19, "Appearance" },
            { 0x1A, "Advertising Interval" },
            { 0x1B, "LE Bluetooth Device Address" },
            { 0x1C, "LE Role" },
            { 0x1F, "32-bit Service Solicitation UUIDs" },
            { 0x20, "32-bit Service Data" },
            { 0x21, "128-bit Service Data" },
            { 0x24, "URI" },
            { 0x25, "Indoor Positioning" },
            { 0x26, "Transport Discovery Data" },
            { 0x27, "LE Supported Features" },
            { 0x28, "Channel Map Update Indication" },
            { 0x2D, "BIGInfo" },
            { 0x2E, "Broadcast Code" },
            { 0x3D, "3D Information Data" },
            { 0xFF, "Manufacturer Specific Data" },
        };

        // A useful selection of company identifiers (Bluetooth SIG).
        private static readonly Dictionary<ushort, string> Companies = new Dictionary<ushort, string>
        {
            { 0x0000, "Ericsson" },
            { 0x0001, "Nokia Mobile Phones" },
            { 0x0002, "Intel" },
            { 0x0006, "Microsoft" },
            { 0x000A, "Cambridge Silicon Radio" },
            { 0x000D, "Texas Instruments" },
            { 0x004C, "Apple, Inc." },
            { 0x0059, "Nordic Semiconductor ASA" },
            { 0x0075, "Samsung Electronics" },
            { 0x0087, "Garmin International" },
            { 0x00E0, "Google" },
            { 0x0118, "Xiaomi Inc." },
            { 0x0157, "Anhui Huami (Amazfit)" },
            { 0x0171, "Amazon.com Services" },
            { 0x0499, "Ruuvi Innovations" },
            { 0x06D5, "Espressif Systems" },
            { 0xFFFF, "Reserved / Test (0xFFFF)" },
        };
        #endregion

        public static bool IsShortUuid(Guid uuid, out ushort shortValue)
        {
            shortValue = 0;
            byte[] b = uuid.ToByteArray(); // little-endian Data1/2/3 + Data4
            // Reconstruct the canonical big-endian byte order to compare the tail.
            byte[] be = new byte[16];
            be[0] = b[3]; be[1] = b[2]; be[2] = b[1]; be[3] = b[0];
            be[4] = b[5]; be[5] = b[4];
            be[6] = b[7]; be[7] = b[6];
            for (int i = 8; i < 16; i++) be[i] = b[i];

            for (int i = 4; i < 16; i++)
                if (be[i] != BaseTail[i - 4]) return false;
            if (be[0] != 0 || be[1] != 0) return false; // 16-bit (not 32-bit) range
            shortValue = (ushort)((be[2] << 8) | be[3]);
            return true;
        }

        public static string ServiceName(Guid uuid)
        {
            ushort s;
            if (IsShortUuid(uuid, out s))
            {
                string name;
                if (Services.TryGetValue(s, out name))
                    return name + " (0x" + s.ToString("X4") + ")";
                return "Unknown Service (0x" + s.ToString("X4") + ")";
            }
            return "Custom Service (" + uuid.ToString().ToUpperInvariant() + ")";
        }

        public static string CharacteristicName(Guid uuid)
        {
            ushort s;
            if (IsShortUuid(uuid, out s))
            {
                string name;
                if (Characteristics.TryGetValue(s, out name))
                    return name + " (0x" + s.ToString("X4") + ")";
                return "Unknown Characteristic (0x" + s.ToString("X4") + ")";
            }
            return "Custom Characteristic (" + uuid.ToString().ToUpperInvariant() + ")";
        }

        public static string DescriptorName(Guid uuid)
        {
            ushort s;
            if (IsShortUuid(uuid, out s))
            {
                string name;
                if (Descriptors.TryGetValue(s, out name))
                    return name + " (0x" + s.ToString("X4") + ")";
                return "Descriptor (0x" + s.ToString("X4") + ")";
            }
            return "Custom Descriptor (" + uuid.ToString().ToUpperInvariant() + ")";
        }

        public static string ShortName(Guid uuid)
        {
            ushort s;
            if (IsShortUuid(uuid, out s)) return "0x" + s.ToString("X4");
            return uuid.ToString().ToUpperInvariant();
        }

        public static string CompanyName(ushort id)
        {
            string name;
            if (Companies.TryGetValue(id, out name)) return name + " (0x" + id.ToString("X4") + ")";
            return "Company 0x" + id.ToString("X4");
        }

        public static string AdTypeName(byte type)
        {
            string name;
            if (AdTypes.TryGetValue(type, out name)) return name;
            return "Unknown AD Type";
        }

        /// <summary>Best-effort human interpretation of an advertising section payload.</summary>
        public static string InterpretAdSection(byte type, byte[] data)
        {
            if (data == null) data = new byte[0];
            switch (type)
            {
                case 0x01: // Flags
                    return data.Length > 0 ? DescribeFlags(data[0]) : string.Empty;
                case 0x08:
                case 0x09: // local name
                    return Encoding.UTF8.GetString(data);
                case 0x0A: // tx power
                    return data.Length > 0 ? ((sbyte)data[0]) + " dBm" : string.Empty;
                case 0x19: // appearance
                    return data.Length >= 2 ? "0x" + ((ushort)(data[0] | (data[1] << 8))).ToString("X4") : string.Empty;
                case 0x02:
                case 0x03: // 16-bit UUID list
                    return Join16BitUuids(data);
                case 0x06:
                case 0x07: // 128-bit UUID list
                    return Join128BitUuids(data);
                case 0x16: // 16-bit service data
                    if (data.Length >= 2)
                    {
                        ushort u = (ushort)(data[0] | (data[1] << 8));
                        byte[] rest = new byte[data.Length - 2];
                        Array.Copy(data, 2, rest, 0, rest.Length);
                        return "0x" + u.ToString("X4") + " : " + HexUtil.ToHex(rest);
                    }
                    return HexUtil.ToHex(data);
                case 0xFF: // manufacturer specific
                    if (data.Length >= 2)
                    {
                        ushort company = (ushort)(data[0] | (data[1] << 8));
                        byte[] rest = new byte[data.Length - 2];
                        Array.Copy(data, 2, rest, 0, rest.Length);
                        return CompanyName(company) + " : " + HexUtil.ToHex(rest);
                    }
                    return HexUtil.ToHex(data);
                case 0x24: // URI
                    return Encoding.UTF8.GetString(data);
                default:
                    return HexUtil.ToHex(data);
            }
        }

        private static string DescribeFlags(byte f)
        {
            var parts = new List<string>();
            if ((f & 0x01) != 0) parts.Add("LE Limited Discoverable");
            if ((f & 0x02) != 0) parts.Add("LE General Discoverable");
            if ((f & 0x04) != 0) parts.Add("BR/EDR Not Supported");
            if ((f & 0x08) != 0) parts.Add("LE+BR/EDR (Controller)");
            if ((f & 0x10) != 0) parts.Add("LE+BR/EDR (Host)");
            string flags = parts.Count == 0 ? "none" : string.Join(", ", parts.ToArray());
            return "0x" + f.ToString("X2") + " [" + flags + "]";
        }

        private static string Join16BitUuids(byte[] data)
        {
            var sb = new StringBuilder();
            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                ushort u = (ushort)(data[i] | (data[i + 1] << 8));
                if (sb.Length > 0) sb.Append(", ");
                string name;
                sb.Append(Services.TryGetValue(u, out name) ? name : "0x" + u.ToString("X4"));
            }
            return sb.ToString();
        }

        private static string Join128BitUuids(byte[] data)
        {
            var sb = new StringBuilder();
            for (int i = 0; i + 15 < data.Length; i += 16)
            {
                byte[] le = new byte[16];
                Array.Copy(data, i, le, 0, 16);
                Guid g = GuidFromLittleEndian(le);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(g.ToString().ToUpperInvariant());
            }
            return sb.ToString();
        }

        /// <summary>Builds a Guid from 16 bytes in BLE on-air (little-endian) order.</summary>
        public static Guid GuidFromLittleEndian(byte[] le)
        {
            byte[] be = new byte[16];
            for (int i = 0; i < 16; i++) be[i] = le[15 - i];
            // be is now big-endian canonical; construct a Guid.
            int a = (be[0] << 24) | (be[1] << 16) | (be[2] << 8) | be[3];
            short b = (short)((be[4] << 8) | be[5]);
            short c = (short)((be[6] << 8) | be[7]);
            byte[] d = new byte[8];
            Array.Copy(be, 8, d, 0, 8);
            return new Guid(a, b, c, d);
        }

        public static Guid GuidFrom16Bit(ushort value)
        {
            byte[] be =
            {
                0x00, 0x00, (byte)(value >> 8), (byte)(value & 0xFF),
                0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5F, 0x9B, 0x34, 0xFB
            };
            int a = (be[0] << 24) | (be[1] << 16) | (be[2] << 8) | be[3];
            short b = (short)((be[4] << 8) | be[5]);
            short c = (short)((be[6] << 8) | be[7]);
            byte[] d = new byte[8];
            Array.Copy(be, 8, d, 0, 8);
            return new Guid(a, b, c, d);
        }

        public static bool TryParseUuid(string text, out Guid uuid)
        {
            uuid = Guid.Empty;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            // 16-bit or 32-bit short form.
            string compact = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;
            if (compact.Length == 4 && HexUtil.IsHex(compact))
            {
                ushort v = ushort.Parse(compact, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                uuid = GuidFrom16Bit(v);
                return true;
            }
            return Guid.TryParse(text, out uuid);
        }
    }
}
