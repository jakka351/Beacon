using System;
using BleWorkbench.Core;

namespace BleWorkbench.Models
{
    /// <summary>
    /// One captured/observed unit of BLE traffic. Advertising reports, GATT
    /// reads/writes/notifications and decoded HCI frames are all normalised into
    /// this record so the sniffer can present them in a single timeline.
    /// </summary>
    public class BlePacket
    {
        public long Index { get; set; }
        public DateTime Timestamp { get; set; }
        public PacketSource Source { get; set; }
        public PacketDirection Direction { get; set; }

        /// <summary>Peer device address in "AA:BB:.." form, or empty.</summary>
        public string Address { get; set; }

        /// <summary>Short classification, e.g. "ADV_IND", "Notify", "HCI Event".</summary>
        public string PacketType { get; set; }

        /// <summary>Received signal strength in dBm where available (advertising).</summary>
        public int? Rssi { get; set; }

        public byte? Channel { get; set; }

        /// <summary>One-line human summary.</summary>
        public string Summary { get; set; }

        /// <summary>Raw bytes for the hex viewer / export. May be empty for synthetic events.</summary>
        public byte[] Raw { get; set; }

        public BlePacket()
        {
            Timestamp = DateTime.Now;
            Address = string.Empty;
            PacketType = string.Empty;
            Summary = string.Empty;
            Raw = new byte[0];
        }

        public string SourceText
        {
            get
            {
                switch (Source)
                {
                    case PacketSource.WinRtAdvertisement: return "ADV";
                    case PacketSource.WinRtGatt: return "GATT";
                    case PacketSource.WinRtPeripheral: return "PERIPH";
                    case PacketSource.SerialHci: return "HCI";
                    default: return "SYS";
                }
            }
        }

        public string DirectionText
        {
            get
            {
                switch (Direction)
                {
                    case PacketDirection.Rx: return "RX";
                    case PacketDirection.Tx: return "TX";
                    default: return "--";
                }
            }
        }

        public string TimeText
        {
            get { return Timestamp.ToString("HH:mm:ss.fff"); }
        }

        public string LengthText
        {
            get { return (Raw == null ? 0 : Raw.Length).ToString(); }
        }

        public string RssiText
        {
            get { return Rssi.HasValue ? Rssi.Value.ToString() : string.Empty; }
        }

        public string HexDump
        {
            get { return HexUtil.ToHexDump(Raw); }
        }
    }
}
