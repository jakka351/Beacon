using System;
using System.Globalization;

namespace BleWorkbench.Core
{
    /// <summary>
    /// Conversions for the 48-bit Bluetooth device address. WinRT exposes the
    /// address as a <see cref="ulong"/>; the UI and logs use the canonical
    /// big-endian "AA:BB:CC:DD:EE:FF" form.
    /// </summary>
    public static class BleAddress
    {
        public static string Format(ulong address)
        {
            byte[] b = new byte[6];
            for (int i = 0; i < 6; i++)
                b[i] = (byte)((address >> (8 * (5 - i))) & 0xFF);
            return string.Format(CultureInfo.InvariantCulture,
                "{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                b[0], b[1], b[2], b[3], b[4], b[5]);
        }

        public static ulong Parse(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return 0;
            string clean = mac.Replace(":", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
            if (clean.Length != 12) throw new FormatException("MAC address must be 6 bytes (12 hex digits).");
            ulong value = 0;
            for (int i = 0; i < 6; i++)
            {
                byte octet = byte.Parse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                value = (value << 8) | octet;
            }
            return value;
        }

        public static bool TryParse(string mac, out ulong value)
        {
            value = 0;
            try { value = Parse(mac); return true; }
            catch { return false; }
        }

        /// <summary>Address bytes in transmission order (LSB first), as seen on-air / in HCI.</summary>
        public static byte[] ToLittleEndianBytes(ulong address)
        {
            byte[] b = new byte[6];
            for (int i = 0; i < 6; i++)
                b[i] = (byte)((address >> (8 * i)) & 0xFF);
            return b;
        }

        public static ulong FromLittleEndianBytes(byte[] data, int offset)
        {
            ulong value = 0;
            for (int i = 5; i >= 0; i--)
                value = (value << 8) | data[offset + i];
            return value;
        }

        /// <summary>
        /// Classifies a random address by its two most significant bits, per the
        /// Bluetooth Core specification (Vol 6, Part B, 1.3).
        /// </summary>
        public static string DescribeRandomType(ulong address)
        {
            byte msb = (byte)((address >> 40) & 0xFF);
            int top = msb >> 6;
            switch (top)
            {
                case 0b11: return "Static Random";
                case 0b00: return "Non-resolvable Private";
                case 0b01: return "Resolvable Private";
                default: return "Reserved";
            }
        }
    }
}
