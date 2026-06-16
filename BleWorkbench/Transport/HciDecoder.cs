using System;
using System.Collections.Generic;
using BleWorkbench.Core;
using BleWorkbench.Models;

namespace BleWorkbench.Transport
{
    /// <summary>
    /// Interprets HCI frames produced by <see cref="HciFramer"/>. LE Advertising
    /// Reports are turned into device-registry updates; every frame is logged as a
    /// <see cref="BlePacket"/> so the sniffer shows raw external-adapter traffic.
    /// </summary>
    public class HciDecoder
    {
        private readonly DeviceRegistry _registry;
        private readonly PacketLog _packets;

        public HciDecoder(DeviceRegistry registry, PacketLog packets)
        {
            _registry = registry;
            _packets = packets;
        }

        public void Decode(HciFrame frame)
        {
            string type;
            string summary;
            switch (frame.Type)
            {
                case HciPacketType.Event:
                    DecodeEvent(frame, out type, out summary);
                    break;
                case HciPacketType.Command:
                    type = "HCI Cmd";
                    summary = DescribeCommand(frame.Payload);
                    break;
                case HciPacketType.AclData:
                    type = "ACL";
                    summary = "ACL data, " + Math.Max(0, frame.Payload.Length - 4) + " bytes";
                    break;
                case HciPacketType.ScoData:
                    type = "SCO";
                    summary = "SCO data";
                    break;
                case HciPacketType.IsoData:
                    type = "ISO";
                    summary = "ISO data";
                    break;
                default:
                    type = "HCI";
                    summary = "Unknown frame";
                    break;
            }

            if (_packets != null)
            {
                _packets.Add(new BlePacket
                {
                    Source = PacketSource.SerialHci,
                    Direction = frame.Type == HciPacketType.Command ? PacketDirection.Tx : PacketDirection.Rx,
                    PacketType = type,
                    Raw = frame.FullFrame,
                    Summary = summary
                });
            }
        }

        private void DecodeEvent(HciFrame frame, out string type, out string summary)
        {
            byte[] p = frame.Payload; // [code][plen][params...]
            if (p.Length < 2) { type = "HCI Event"; summary = "(truncated)"; return; }
            byte code = p[0];
            int paramLen = p[1];
            int baseOff = 2;

            switch (code)
            {
                case 0x0E:
                    type = "Cmd Complete";
                    summary = "Command Complete";
                    return;
                case 0x0F:
                    type = "Cmd Status";
                    summary = "Command Status: 0x" + (p.Length > 2 ? p[2].ToString("X2") : "??");
                    return;
                case 0x05:
                    type = "Disconnect";
                    summary = "Disconnection Complete";
                    return;
                case 0x3E:
                    DecodeLeMeta(p, baseOff, out type, out summary);
                    return;
                default:
                    type = "HCI Event";
                    summary = "Event 0x" + code.ToString("X2") + ", " + paramLen + " param bytes";
                    return;
            }
        }

        private void DecodeLeMeta(byte[] p, int off, out string type, out string summary)
        {
            if (off >= p.Length) { type = "LE Meta"; summary = "(truncated)"; return; }
            byte sub = p[off];
            switch (sub)
            {
                case 0x02:
                    type = "LE Adv Report";
                    summary = DecodeAdvReports(p, off + 1, false);
                    return;
                case 0x0D:
                    type = "LE Ext Adv";
                    summary = DecodeAdvReports(p, off + 1, true);
                    return;
                case 0x01:
                    type = "LE Conn";
                    summary = "LE Connection Complete";
                    return;
                case 0x03:
                    type = "LE Conn Upd";
                    summary = "LE Connection Update Complete";
                    return;
                default:
                    type = "LE Meta";
                    summary = "LE subevent 0x" + sub.ToString("X2");
                    return;
            }
        }

        private string DecodeAdvReports(byte[] p, int off, bool extended)
        {
            if (off >= p.Length) return "(truncated)";
            int numReports = p[off++];
            int parsed = 0;
            string lastSummary = "";

            for (int r = 0; r < numReports && off < p.Length; r++)
            {
                try
                {
                    if (extended)
                        off = ParseExtendedReport(p, off, out lastSummary);
                    else
                        off = ParseLegacyReport(p, off, out lastSummary);
                    parsed++;
                }
                catch
                {
                    break;
                }
            }
            return numReports > 1 ? (parsed + " reports; " + lastSummary) : lastSummary;
        }

        private int ParseLegacyReport(byte[] p, int off, out string summary)
        {
            byte eventType = p[off + 0];
            byte addrType = p[off + 1];
            ulong addr = BleAddress.FromLittleEndianBytes(p, off + 2);
            int dataLen = p[off + 8];
            int dataStart = off + 9;
            var adData = new byte[dataLen];
            Array.Copy(p, dataStart, adData, 0, dataLen);
            sbyte rssi = (sbyte)p[dataStart + dataLen];
            int next = dataStart + dataLen + 1;

            summary = UpdateDevice(addr, addrType, LegacyTypeName(eventType), MapLegacyType(eventType), adData, rssi);
            return next;
        }

        private int ParseExtendedReport(byte[] p, int off, out string summary)
        {
            // Event_Type(2), Addr_Type(1), Address(6), Primary_PHY(1), Secondary_PHY(1),
            // SID(1), Tx_Power(1), RSSI(1), Periodic_Interval(2), Direct_Addr_Type(1),
            // Direct_Address(6), Data_Length(1), Data(Data_Length)
            byte addrType = p[off + 2];
            ulong addr = BleAddress.FromLittleEndianBytes(p, off + 3);
            sbyte rssi = (sbyte)p[off + 12];
            int dataLen = p[off + 23];
            int dataStart = off + 24;
            var adData = new byte[dataLen];
            Array.Copy(p, dataStart, adData, 0, dataLen);
            int next = dataStart + dataLen;

            summary = UpdateDevice(addr, addrType, "ADV_EXT_IND", AdvType.Extended, adData, rssi);
            return next;
        }

        private string UpdateDevice(ulong addr, byte addrType, string typeName, AdvType advType, byte[] adData, sbyte rssi)
        {
            BleDeviceInfo info = _registry.GetOrAdd(addr);
            info.LastSeen = DateTime.Now;
            info.AdvCount++;
            info.Rssi = rssi;
            info.LastAdvType = advType;
            info.Connectable = advType == AdvType.ConnectableUndirected || advType == AdvType.ConnectableDirected;
            info.AddressKind = (addrType == 0x01 || addrType == 0x03) ? AddressKind.Random : AddressKind.Public;

            var sections = AdvertisementParser.ParseSections(adData);
            AdvertisementParser.ApplyToDevice(info, sections);

            // Record the AD payload as its own packet for the hex viewer.
            if (_packets != null && adData.Length > 0)
            {
                _packets.Add(new BlePacket
                {
                    Source = PacketSource.SerialHci,
                    Direction = PacketDirection.Rx,
                    Address = info.AddressText,
                    PacketType = typeName,
                    Rssi = rssi,
                    Raw = adData,
                    Summary = (string.IsNullOrEmpty(info.Name) ? "(no name)" : info.Name)
                });
            }

            _registry.RaiseUpdated(info);
            return info.AddressText + "  " + typeName + "  rssi=" + rssi +
                   (string.IsNullOrEmpty(info.Name) ? "" : "  '" + info.Name + "'");
        }

        private static string LegacyTypeName(byte t)
        {
            switch (t)
            {
                case 0x00: return "ADV_IND";
                case 0x01: return "ADV_DIRECT_IND";
                case 0x02: return "ADV_SCAN_IND";
                case 0x03: return "ADV_NONCONN_IND";
                case 0x04: return "SCAN_RSP";
                default: return "ADV";
            }
        }

        private static AdvType MapLegacyType(byte t)
        {
            switch (t)
            {
                case 0x00: return AdvType.ConnectableUndirected;
                case 0x01: return AdvType.ConnectableDirected;
                case 0x02: return AdvType.ScannableUndirected;
                case 0x03: return AdvType.NonConnectableUndirected;
                case 0x04: return AdvType.ScanResponse;
                default: return AdvType.Unknown;
            }
        }

        private static string DescribeCommand(byte[] payload)
        {
            if (payload.Length < 2) return "HCI command";
            ushort opcode = (ushort)(payload[0] | (payload[1] << 8));
            int ogf = opcode >> 10;
            int ocf = opcode & 0x3FF;
            return "OpCode 0x" + opcode.ToString("X4") + " (OGF=0x" + ogf.ToString("X2") + ", OCF=0x" + ocf.ToString("X3") + ")";
        }
    }
}
