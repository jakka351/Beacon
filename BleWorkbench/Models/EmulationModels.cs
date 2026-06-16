using System;
using System.Collections.Generic;
using BleWorkbench.Core;

namespace BleWorkbench.Models
{
    /// <summary>Definition of one characteristic to expose on an emulated peripheral.</summary>
    public class CharacteristicDefinition
    {
        public Guid Uuid { get; set; }
        public bool Read { get; set; }
        public bool Write { get; set; }
        public bool WriteNoResponse { get; set; }
        public bool Notify { get; set; }
        public bool Indicate { get; set; }
        public byte[] InitialValue { get; set; }
        public string UserDescription { get; set; }

        public CharacteristicDefinition()
        {
            InitialValue = new byte[0];
            UserDescription = string.Empty;
            Read = true;
        }

        public string PropertiesText
        {
            get
            {
                var p = new List<string>();
                if (Read) p.Add("Read");
                if (Write) p.Add("Write");
                if (WriteNoResponse) p.Add("WriteNoResp");
                if (Notify) p.Add("Notify");
                if (Indicate) p.Add("Indicate");
                return p.Count == 0 ? "(none)" : string.Join(" | ", p.ToArray());
            }
        }

        public string UuidText { get { return AssignedNumbers.ShortName(Uuid); } }
        public string InitialHex { get { return HexUtil.ToHex(InitialValue); } }
    }

    /// <summary>Definition of an emulated peripheral (one primary service).</summary>
    public class PeripheralDefinition
    {
        public string LocalName { get; set; }
        public Guid ServiceUuid { get; set; }
        public bool Connectable { get; set; }
        public bool Discoverable { get; set; }
        public List<CharacteristicDefinition> Characteristics { get; private set; }

        public PeripheralDefinition()
        {
            LocalName = "BLE-Workbench";
            Connectable = true;
            Discoverable = true;
            Characteristics = new List<CharacteristicDefinition>();
        }
    }

    /// <summary>Definition of a broadcast-only advertising payload.</summary>
    public class AdvertisementDefinition
    {
        public string LocalName { get; set; }
        public ushort? CompanyId { get; set; }
        public byte[] ManufacturerData { get; set; }
        public List<Guid> ServiceUuids { get; private set; }
        /// <summary>Extra raw AD sections: (dataType, data).</summary>
        public List<KeyValuePair<byte, byte[]>> ExtraSections { get; private set; }

        public AdvertisementDefinition()
        {
            LocalName = string.Empty;
            ServiceUuids = new List<Guid>();
            ExtraSections = new List<KeyValuePair<byte, byte[]>>();
        }
    }
}
