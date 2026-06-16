using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BleWorkbench.Models;

namespace BleWorkbench.Core
{
    /// <summary>
    /// Exports captured packets. CSV / text cover every record; BTSnoop produces
    /// a Wireshark-readable HCI capture from advertising and HCI-sourced packets
    /// (legacy advertising reports are synthesised as HCI LE Advertising Report
    /// events so the file opens cleanly in Bluetooth tooling).
    /// </summary>
    public static class CaptureExport
    {
        // Microseconds between 0000-01-01 (BTSnoop epoch) and 1970-01-01 (Unix epoch).
        private const long BtSnoopEpochDelta = 0x00dcddb30f2f8000L;
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void ToCsv(string path, IEnumerable<BlePacket> packets)
        {
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            {
                w.WriteLine("Index,Time,Source,Direction,Type,Address,RSSI,Length,Summary,HexData");
                foreach (var p in packets)
                {
                    w.WriteLine(string.Join(",",
                        p.Index.ToString(CultureInfo.InvariantCulture),
                        Csv(p.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")),
                        Csv(p.SourceText),
                        Csv(p.DirectionText),
                        Csv(p.PacketType),
                        Csv(p.Address),
                        Csv(p.RssiText),
                        p.LengthText,
                        Csv(p.Summary),
                        Csv(HexUtil.ToHexCompact(p.Raw))));
                }
            }
        }

        public static void ToText(string path, IEnumerable<BlePacket> packets)
        {
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            {
                foreach (var p in packets)
                {
                    w.WriteLine("[" + p.TimeText + "] #" + p.Index + "  " + p.SourceText + "/" + p.DirectionText +
                                "  " + p.PacketType + "  " + p.Address +
                                (p.Rssi.HasValue ? "  " + p.Rssi + " dBm" : "") +
                                "  (" + p.LengthText + " bytes)");
                    if (!string.IsNullOrEmpty(p.Summary)) w.WriteLine("    " + p.Summary);
                    if (p.Raw != null && p.Raw.Length > 0) w.WriteLine(Indent(HexUtil.ToHexDump(p.Raw)));
                    w.WriteLine();
                }
            }
        }

        public static int ToBtSnoop(string path, IEnumerable<BlePacket> packets)
        {
            int written = 0;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                // Header: "btsnoop\0", version=1, datalink=1001 (HCI UART / H4) — all big-endian.
                fs.Write(Encoding.ASCII.GetBytes("btsnoop\0"), 0, 8);
                WriteBE32(fs, 1);
                WriteBE32(fs, 1001);

                foreach (var p in packets)
                {
                    byte[] frame; int flags;
                    if (!BuildHciFrame(p, out frame, out flags)) continue;

                    long ts = BtSnoopEpochDelta + (p.Timestamp.ToUniversalTime() - UnixEpoch).Ticks / 10;
                    WriteBE32(fs, frame.Length);   // original length
                    WriteBE32(fs, frame.Length);   // included length
                    WriteBE32(fs, flags);          // packet flags
                    WriteBE32(fs, 0);              // cumulative drops
                    WriteBE64(fs, ts);            // timestamp (microseconds)
                    fs.Write(frame, 0, frame.Length);
                    written++;
                }
            }
            return written;
        }

        /// <summary>Returns an H4 frame (type byte + HCI payload) and the BTSnoop direction/type flags.</summary>
        private static bool BuildHciFrame(BlePacket p, out byte[] frame, out int flags)
        {
            frame = null;
            flags = 0;

            if (p.Source == PacketSource.SerialHci && p.Raw != null && p.Raw.Length >= 2 &&
                (p.Raw[0] == 0x01 || p.Raw[0] == 0x02 || p.Raw[0] == 0x04))
            {
                frame = p.Raw;
                bool received = p.Raw[0] != 0x01;             // commands are host->controller
                bool cmdEvt = p.Raw[0] == 0x01 || p.Raw[0] == 0x04;
                flags = (received ? 1 : 0) | (cmdEvt ? 2 : 0);
                return true;
            }

            if (p.Source == PacketSource.WinRtAdvertisement)
            {
                frame = SynthesizeAdvReport(p);
                flags = 0x03; // received + event
                return frame != null;
            }
            return false;
        }

        private static byte[] SynthesizeAdvReport(BlePacket p)
        {
            byte[] ad = p.Raw ?? new byte[0];
            if (ad.Length > 31) { var t = new byte[31]; Array.Copy(ad, t, 31); ad = t; }

            ulong addr;
            if (!BleAddress.TryParse(p.Address, out addr)) addr = 0;
            byte[] addrLe = BleAddress.ToLittleEndianBytes(addr);
            byte evtType = AdvTypeCode(p.PacketType);
            sbyte rssi = (sbyte)(p.Rssi ?? 0);

            using (var ms = new MemoryStream())
            {
                // HCI report parameters: subevent, num_reports, event_type, addr_type, addr, len, data, rssi
                var rep = new MemoryStream();
                rep.WriteByte(0x02);                 // subevent: LE Advertising Report
                rep.WriteByte(0x01);                 // num reports
                rep.WriteByte(evtType);
                rep.WriteByte(0x00);                 // address type: public (unknown from WinRT here)
                rep.Write(addrLe, 0, 6);
                rep.WriteByte((byte)ad.Length);
                rep.Write(ad, 0, ad.Length);
                rep.WriteByte((byte)rssi);
                byte[] repBytes = rep.ToArray();

                ms.WriteByte(0x04);                  // H4 type: event
                ms.WriteByte(0x3E);                  // event code: LE Meta
                ms.WriteByte((byte)repBytes.Length); // parameter length
                ms.Write(repBytes, 0, repBytes.Length);
                return ms.ToArray();
            }
        }

        private static byte AdvTypeCode(string typeName)
        {
            switch (typeName)
            {
                case "ADV_IND": return 0x00;
                case "ADV_DIRECT_IND": return 0x01;
                case "ADV_SCAN_IND": return 0x02;
                case "ADV_NONCONN_IND": return 0x03;
                case "SCAN_RSP": return 0x04;
                default: return 0x00;
            }
        }

        private static void WriteBE32(Stream s, int v)
        {
            s.WriteByte((byte)(v >> 24));
            s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)v);
        }

        private static void WriteBE64(Stream s, long v)
        {
            for (int i = 7; i >= 0; i--) s.WriteByte((byte)(v >> (8 * i)));
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string Indent(string block)
        {
            var sb = new StringBuilder();
            foreach (var line in block.Split('\n'))
                if (line.Length > 0) sb.Append("    ").Append(line).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }
    }
}
