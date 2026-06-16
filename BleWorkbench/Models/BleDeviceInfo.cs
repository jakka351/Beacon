using System;
using System.Collections.Generic;
using System.Text;
using BleWorkbench.Core;

namespace BleWorkbench.Models
{
    /// <summary>
    /// Aggregated view of a discovered device built up from successive
    /// advertising reports (and, once connected, GATT discovery).
    /// </summary>
    public class BleDeviceInfo
    {
        public ulong Address { get; set; }
        public AddressKind AddressKind { get; set; }
        public string Name { get; set; }
        public int Rssi { get; set; }
        public short? TxPower { get; set; }
        public AdvType LastAdvType { get; set; }
        public bool Connectable { get; set; }

        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public long AdvCount { get; set; }

        /// <summary>Company identifier -> manufacturer specific data (latest seen).</summary>
        public Dictionary<ushort, byte[]> ManufacturerData { get; private set; }

        /// <summary>Advertised service UUIDs (as canonical strings).</summary>
        public List<Guid> ServiceUuids { get; private set; }

        /// <summary>Raw AD structures from the most recent payload.</summary>
        public List<AdvertisementSection> Sections { get; private set; }

        public BleDeviceInfo()
        {
            Name = string.Empty;
            ManufacturerData = new Dictionary<ushort, byte[]>();
            ServiceUuids = new List<Guid>();
            Sections = new List<AdvertisementSection>();
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;
            AddressKind = AddressKind.Unspecified;
        }

        public string AddressText { get { return BleAddress.Format(Address); } }

        public string AddressTypeText
        {
            get
            {
                if (AddressKind == AddressKind.Random)
                    return "Random (" + BleAddress.DescribeRandomType(Address) + ")";
                if (AddressKind == AddressKind.Public)
                    return "Public";
                return "Unknown";
            }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(Name) ? "(unknown)" : Name; }
        }

        public string CompanyText
        {
            get
            {
                if (ManufacturerData.Count == 0) return string.Empty;
                var sb = new StringBuilder();
                foreach (var kv in ManufacturerData)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(AssignedNumbers.CompanyName(kv.Key));
                }
                return sb.ToString();
            }
        }

        public string ServicesText
        {
            get
            {
                if (ServiceUuids.Count == 0) return string.Empty;
                var sb = new StringBuilder();
                foreach (var g in ServiceUuids)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(AssignedNumbers.ServiceName(g));
                }
                return sb.ToString();
            }
        }

        public string LastSeenText { get { return LastSeen.ToString("HH:mm:ss"); } }

        public string AdvTypeText
        {
            get
            {
                switch (LastAdvType)
                {
                    case AdvType.ConnectableUndirected: return "ADV_IND";
                    case AdvType.ConnectableDirected: return "ADV_DIRECT_IND";
                    case AdvType.ScannableUndirected: return "ADV_SCAN_IND";
                    case AdvType.NonConnectableUndirected: return "ADV_NONCONN_IND";
                    case AdvType.ScanResponse: return "SCAN_RSP";
                    case AdvType.Extended: return "ADV_EXT_IND";
                    default: return "";
                }
            }
        }

        public string TxPowerText { get { return TxPower.HasValue ? TxPower.Value + " dBm" : string.Empty; } }

        public string ConnectableText { get { return Connectable ? "Yes" : "No"; } }
    }
}
