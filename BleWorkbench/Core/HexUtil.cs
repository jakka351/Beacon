using System;
using System.Globalization;
using System.Text;

namespace BleWorkbench.Core
{
    /// <summary>
    /// Helpers for converting between byte buffers and the textual representations
    /// used throughout the UI (hex dumps, byte lists, ASCII previews).
    /// </summary>
    public static class HexUtil
    {
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        /// <summary>Space separated upper-case hex, e.g. "01 A2 FF".</summary>
        public static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            var sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(HexChars[data[i] >> 4]);
                sb.Append(HexChars[data[i] & 0x0F]);
            }
            return sb.ToString();
        }

        /// <summary>Contiguous hex with no separators, e.g. "01A2FF".</summary>
        public static string ToHexCompact(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
            {
                sb.Append(HexChars[b >> 4]);
                sb.Append(HexChars[b & 0x0F]);
            }
            return sb.ToString();
        }

        /// <summary>Printable-ASCII preview, non-printable bytes rendered as '.'.</summary>
        public static string ToAscii(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            var sb = new StringBuilder(data.Length);
            foreach (byte b in data)
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            return sb.ToString();
        }

        /// <summary>
        /// Classic offset / hex / ascii dump, 16 bytes per row, as produced by hexdump tools.
        /// </summary>
        public static string ToHexDump(byte[] data, int bytesPerRow = 16)
        {
            if (data == null || data.Length == 0) return "(empty)";
            if (bytesPerRow <= 0) bytesPerRow = 16;
            var sb = new StringBuilder();
            for (int offset = 0; offset < data.Length; offset += bytesPerRow)
            {
                sb.Append(offset.ToString("X4", CultureInfo.InvariantCulture));
                sb.Append("  ");

                for (int i = 0; i < bytesPerRow; i++)
                {
                    int idx = offset + i;
                    if (idx < data.Length)
                    {
                        sb.Append(HexChars[data[idx] >> 4]);
                        sb.Append(HexChars[data[idx] & 0x0F]);
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append("   ");
                    }
                    if (i == 7) sb.Append(' ');
                }

                sb.Append(' ');
                for (int i = 0; i < bytesPerRow; i++)
                {
                    int idx = offset + i;
                    if (idx >= data.Length) break;
                    byte b = data[idx];
                    sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses flexible user input into a byte buffer. Accepts:
        ///   "Hello"          -> UTF-8 text
        ///   "01 A2 FF"       -> space separated hex
        ///   "01,A2,FF"       -> comma separated (decimal unless 0x prefixed)
        ///   "0x41,0x42"      -> comma separated hex
        ///   "01A2FF"         -> contiguous hex (even length)
        /// </summary>
        public static byte[] ParseInput(string text, bool forceText = false)
        {
            if (string.IsNullOrEmpty(text)) return new byte[0];
            text = text.Trim();
            if (forceText) return Encoding.UTF8.GetBytes(text);

            if (text.IndexOf(',') >= 0)
            {
                string[] parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                bool anyHex = false;
                foreach (var p in parts)
                    if (p.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { anyHex = true; break; }

                var outBytes = new byte[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (p.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) p = p.Substring(2);
                    outBytes[i] = anyHex
                        ? byte.Parse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                        : (byte)(int.Parse(p, CultureInfo.InvariantCulture) & 0xFF);
                }
                return outBytes;
            }

            string collapsed = text.Replace(" ", string.Empty).Replace("-", string.Empty).Replace(":", string.Empty);
            if (collapsed.Length > 0 && collapsed.Length % 2 == 0 && IsHex(collapsed))
                return FromHex(collapsed);

            // Space separated hex tokens (e.g. "01 A2 FF").
            if (text.IndexOf(' ') >= 0)
            {
                string[] tok = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool allHex = true;
                foreach (var t in tok) if (t.Length > 2 || !IsHex(t)) { allHex = false; break; }
                if (allHex)
                {
                    var outBytes = new byte[tok.Length];
                    for (int i = 0; i < tok.Length; i++)
                        outBytes[i] = byte.Parse(tok[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return outBytes;
                }
            }

            return Encoding.UTF8.GetBytes(text);
        }

        public static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            hex = hex.Replace(" ", string.Empty).Replace("-", string.Empty).Replace(":", string.Empty);
            if (hex.Length % 2 != 0) throw new FormatException("Hex string must have an even number of digits.");
            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return result;
        }

        public static bool IsHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }
    }
}
