namespace BleWorkbench.Models
{
    /// <summary>Where a captured packet originated.</summary>
    public enum PacketSource
    {
        WinRtAdvertisement,   // BluetoothLEAdvertisementWatcher report
        WinRtGatt,            // GATT client read/write/notify traffic
        WinRtPeripheral,      // local GATT server request/response
        SerialHci,            // external adapter / sniffer over HCI UART (H4)
        System                // informational, app-generated
    }

    public enum PacketDirection
    {
        Rx,   // received by us / observed on-air
        Tx,   // transmitted by us
        Info  // not directional
    }

    /// <summary>BLE advertising PDU types (matches WinRT BluetoothLEAdvertisementType).</summary>
    public enum AdvType
    {
        ConnectableUndirected = 0,
        ConnectableDirected = 1,
        ScannableUndirected = 2,
        NonConnectableUndirected = 3,
        ScanResponse = 4,
        Extended = 5,
        Unknown = 255
    }

    public enum AddressKind
    {
        Public,
        Random,
        Unspecified
    }

    /// <summary>Role the emulator is currently playing.</summary>
    public enum EmulatorRole
    {
        Off,
        BroadcasterOnly,   // advertising payload, not connectable
        Peripheral         // connectable GATT server
    }
}
