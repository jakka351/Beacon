using BleWorkbench.Core;

namespace BleWorkbench.Models
{
    /// <summary>
    /// A single AD structure from an advertising payload: a one-byte AD type
    /// followed by its data, as defined by the Bluetooth Core Supplement (CSS).
    /// </summary>
    public class AdvertisementSection
    {
        public byte DataType { get; set; }
        public byte[] Data { get; set; }

        public string TypeName
        {
            get { return AssignedNumbers.AdTypeName(DataType); }
        }

        public string TypeCode
        {
            get { return "0x" + DataType.ToString("X2"); }
        }

        public string DataHex
        {
            get { return HexUtil.ToHex(Data); }
        }

        /// <summary>
        /// A human-friendly interpretation of the section payload where one is
        /// known (flags, names, UUID lists, etc.); otherwise hex.
        /// </summary>
        public string Interpreted
        {
            get { return AssignedNumbers.InterpretAdSection(DataType, Data); }
        }
    }
}
