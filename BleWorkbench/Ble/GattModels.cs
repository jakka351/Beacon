using System;
using System.Collections.Generic;
using System.Text;
using BleWorkbench.Core;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleWorkbench.Ble
{
    /// <summary>A discovered GATT service plus its characteristics.</summary>
    public class GattServiceView
    {
        public Guid Uuid { get; set; }
        public GattDeviceService Service { get; set; }
        public List<GattCharacteristicView> Characteristics { get; private set; }

        public GattServiceView() { Characteristics = new List<GattCharacteristicView>(); }

        public string DisplayName { get { return AssignedNumbers.ServiceName(Uuid); } }
    }

    /// <summary>A discovered characteristic, its properties and descriptors.</summary>
    public class GattCharacteristicView
    {
        public Guid Uuid { get; set; }
        public GattCharacteristic Characteristic { get; set; }
        public GattCharacteristicProperties Properties { get; set; }
        public List<GattDescriptorView> Descriptors { get; private set; }

        public GattCharacteristicView() { Descriptors = new List<GattDescriptorView>(); }

        public string DisplayName { get { return AssignedNumbers.CharacteristicName(Uuid); } }

        public bool CanRead { get { return Has(GattCharacteristicProperties.Read); } }
        public bool CanWrite { get { return Has(GattCharacteristicProperties.Write); } }
        public bool CanWriteNoResponse { get { return Has(GattCharacteristicProperties.WriteWithoutResponse); } }
        public bool CanNotify { get { return Has(GattCharacteristicProperties.Notify); } }
        public bool CanIndicate { get { return Has(GattCharacteristicProperties.Indicate); } }

        private bool Has(GattCharacteristicProperties p) { return (Properties & p) == p; }

        public string PropertiesText
        {
            get
            {
                var parts = new List<string>();
                if (CanRead) parts.Add("Read");
                if (CanWrite) parts.Add("Write");
                if (CanWriteNoResponse) parts.Add("WriteNoResp");
                if (CanNotify) parts.Add("Notify");
                if (CanIndicate) parts.Add("Indicate");
                if (Has(GattCharacteristicProperties.Broadcast)) parts.Add("Broadcast");
                if (Has(GattCharacteristicProperties.AuthenticatedSignedWrites)) parts.Add("SignedWrite");
                if (Has(GattCharacteristicProperties.ExtendedProperties)) parts.Add("ExtProps");
                return parts.Count == 0 ? "(none)" : string.Join(" | ", parts.ToArray());
            }
        }
    }

    /// <summary>A characteristic descriptor (e.g. CCCD, user description).</summary>
    public class GattDescriptorView
    {
        public Guid Uuid { get; set; }
        public GattDescriptor Descriptor { get; set; }

        public string DisplayName { get { return AssignedNumbers.DescriptorName(Uuid); } }
    }
}
