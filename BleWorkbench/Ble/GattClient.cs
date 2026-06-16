using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BleWorkbench.Core;
using BleWorkbench.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;

namespace BleWorkbench.Ble
{
    public class GattNotification
    {
        public GattCharacteristicView Characteristic { get; set; }
        public byte[] Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// GATT client role: connect to a peripheral, enumerate the attribute table
    /// and perform read / write / notify operations. Every operation is mirrored
    /// into the shared packet log.
    /// </summary>
    public class GattClient
    {
        private readonly PacketLog _packets;
        private BluetoothLEDevice _device;
        private GattSession _session;
        private readonly List<GattServiceView> _services = new List<GattServiceView>();
        private readonly Dictionary<GattCharacteristicView, TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>> _subs =
            new Dictionary<GattCharacteristicView, TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>>();

        public event EventHandler<bool> ConnectionChanged;
        public event EventHandler ServicesDiscovered;
        public event EventHandler<GattNotification> Notified;

        public bool IsConnected { get { return _device != null && _device.ConnectionStatus == BluetoothConnectionStatus.Connected; } }
        public ulong Address { get; private set; }
        public string AddressText { get { return BleAddress.Format(Address); } }
        public string DeviceName { get; private set; }
        public IList<GattServiceView> Services { get { return _services; } }

        public GattClient(PacketLog packets) { _packets = packets; }

        public async Task<bool> ConnectAsync(ulong address)
        {
            Disconnect();
            Address = address;
            AppLog.Info("Connecting to " + BleAddress.Format(address) + " ...");

            _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (_device == null)
            {
                AppLog.Error("Could not create a device object for " + BleAddress.Format(address) +
                             ". The device may be out of range or already in use.");
                return false;
            }

            DeviceName = _device.Name;
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;

            try
            {
                _session = await GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId);
                if (_session != null) _session.MaintainConnection = true;
            }
            catch (Exception ex)
            {
                AppLog.Warn("GATT session could not be created: " + ex.Message);
            }

            await DiscoverAsync();

            AppLog.Success("Connected to " + (string.IsNullOrEmpty(DeviceName) ? AddressText : DeviceName) + ".");
            RaiseConnection(true);
            return true;
        }

        public async Task DiscoverAsync()
        {
            _services.Clear();
            if (_device == null) return;

            AppLog.Info("Discovering services ...");
            GattDeviceServicesResult sr = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (sr.Status != GattCommunicationStatus.Success)
            {
                AppLog.Error("Service discovery failed: " + sr.Status);
                return;
            }

            foreach (GattDeviceService svc in sr.Services)
            {
                var sv = new GattServiceView { Uuid = svc.Uuid, Service = svc };
                try
                {
                    GattCharacteristicsResult cr = await svc.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (cr.Status == GattCommunicationStatus.Success)
                    {
                        foreach (GattCharacteristic ch in cr.Characteristics)
                        {
                            var cv = new GattCharacteristicView
                            {
                                Uuid = ch.Uuid,
                                Characteristic = ch,
                                Properties = ch.CharacteristicProperties
                            };
                            try
                            {
                                GattDescriptorsResult dr = await ch.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
                                if (dr.Status == GattCommunicationStatus.Success)
                                    foreach (GattDescriptor d in dr.Descriptors)
                                        cv.Descriptors.Add(new GattDescriptorView { Uuid = d.Uuid, Descriptor = d });
                            }
                            catch { }
                            sv.Characteristics.Add(cv);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Characteristic discovery failed for " + AssignedNumbers.ServiceName(svc.Uuid) + ": " + ex.Message);
                }
                _services.Add(sv);
            }

            int chCount = 0;
            foreach (var s in _services) chCount += s.Characteristics.Count;
            AppLog.Success("Discovered " + _services.Count + " services, " + chCount + " characteristics.");

            var handler = ServicesDiscovered;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public async Task<byte[]> ReadAsync(GattCharacteristicView ch)
        {
            if (ch == null || ch.Characteristic == null) return null;
            GattReadResult r = await ch.Characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (r.Status != GattCommunicationStatus.Success)
            {
                AppLog.Error("Read failed (" + AssignedNumbers.CharacteristicName(ch.Uuid) + "): " + r.Status);
                return null;
            }
            byte[] data = BleBuffers.ToBytes(r.Value);
            RecordGatt(PacketDirection.Rx, "Read Rsp", ch.Uuid, data, "Read " + data.Length + " bytes");
            AppLog.Rx("Read " + AssignedNumbers.CharacteristicName(ch.Uuid) + " -> " + HexUtil.ToHex(data));
            return data;
        }

        public async Task<bool> WriteAsync(GattCharacteristicView ch, byte[] data, bool withResponse)
        {
            if (ch == null || ch.Characteristic == null) return false;
            GattWriteOption opt = withResponse ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse;
            GattWriteResult r = await ch.Characteristic.WriteValueWithResultAsync(BleBuffers.FromBytes(data), opt);
            bool ok = r.Status == GattCommunicationStatus.Success;
            RecordGatt(PacketDirection.Tx, withResponse ? "Write Req" : "Write Cmd", ch.Uuid, data,
                (ok ? "OK " : "FAIL ") + data.Length + " bytes");
            if (ok) AppLog.Tx("Wrote " + HexUtil.ToHex(data) + " -> " + AssignedNumbers.CharacteristicName(ch.Uuid));
            else AppLog.Error("Write failed (" + AssignedNumbers.CharacteristicName(ch.Uuid) + "): " + r.Status);
            return ok;
        }

        /// <summary>Write used by the fuzzer: records a packet but does not flood the console log.</summary>
        public async Task<bool> WriteQuietAsync(GattCharacteristicView ch, byte[] data, bool withResponse)
        {
            if (ch == null || ch.Characteristic == null) return false;
            GattWriteOption opt = withResponse ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse;
            GattWriteResult r = await ch.Characteristic.WriteValueWithResultAsync(BleBuffers.FromBytes(data), opt);
            bool ok = r.Status == GattCommunicationStatus.Success;
            RecordGatt(PacketDirection.Tx, withResponse ? "Write Req" : "Write Cmd", ch.Uuid, data,
                (ok ? "OK " : "FAIL ") + data.Length + " bytes (fuzz)");
            return ok;
        }

        public async Task<bool> SubscribeAsync(GattCharacteristicView ch, bool indicate)
        {
            if (ch == null || ch.Characteristic == null) return false;
            GattClientCharacteristicConfigurationDescriptorValue v = indicate
                ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                : GattClientCharacteristicConfigurationDescriptorValue.Notify;

            GattCommunicationStatus status =
                await ch.Characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(v);
            if (status != GattCommunicationStatus.Success)
            {
                AppLog.Error("Subscribe failed (" + AssignedNumbers.CharacteristicName(ch.Uuid) + "): " + status);
                return false;
            }

            if (!_subs.ContainsKey(ch))
            {
                TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> h = (s, e) => OnValueChanged(ch, e);
                ch.Characteristic.ValueChanged += h;
                _subs[ch] = h;
            }
            AppLog.Success("Subscribed (" + (indicate ? "indicate" : "notify") + ") to " + AssignedNumbers.CharacteristicName(ch.Uuid) + ".");
            return true;
        }

        public async Task<bool> UnsubscribeAsync(GattCharacteristicView ch)
        {
            if (ch == null || ch.Characteristic == null) return false;
            try
            {
                await ch.Characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch { }

            TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> h;
            if (_subs.TryGetValue(ch, out h))
            {
                ch.Characteristic.ValueChanged -= h;
                _subs.Remove(ch);
            }
            AppLog.Info("Unsubscribed from " + AssignedNumbers.CharacteristicName(ch.Uuid) + ".");
            return true;
        }

        public bool IsSubscribed(GattCharacteristicView ch)
        {
            return ch != null && _subs.ContainsKey(ch);
        }

        public async Task<byte[]> ReadDescriptorAsync(GattDescriptorView d)
        {
            if (d == null || d.Descriptor == null) return null;
            GattReadResult r = await d.Descriptor.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (r.Status != GattCommunicationStatus.Success) return null;
            return BleBuffers.ToBytes(r.Value);
        }

        private void OnValueChanged(GattCharacteristicView ch, GattValueChangedEventArgs e)
        {
            byte[] data = BleBuffers.ToBytes(e.CharacteristicValue);
            RecordGatt(PacketDirection.Rx, "Notify", ch.Uuid, data, "Notification " + data.Length + " bytes");
            AppLog.Rx("Notify " + AssignedNumbers.CharacteristicName(ch.Uuid) + " -> " + HexUtil.ToHex(data));

            var handler = Notified;
            if (handler != null)
                handler(this, new GattNotification { Characteristic = ch, Value = data, Timestamp = DateTime.Now });
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            bool connected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
            AppLog.Info("Connection status: " + (connected ? "Connected" : "Disconnected"));
            RaiseConnection(connected);
        }

        private void RecordGatt(PacketDirection dir, string type, Guid uuid, byte[] data, string summary)
        {
            if (_packets == null) return;
            _packets.Add(new BlePacket
            {
                Source = PacketSource.WinRtGatt,
                Direction = dir,
                Address = AddressText,
                PacketType = type,
                Raw = data ?? new byte[0],
                Summary = AssignedNumbers.CharacteristicName(uuid) + "  " + summary
            });
        }

        public void Disconnect()
        {
            foreach (var kv in _subs)
            {
                try { kv.Key.Characteristic.ValueChanged -= kv.Value; } catch { }
            }
            _subs.Clear();

            if (_session != null)
            {
                try { _session.MaintainConnection = false; _session.Dispose(); } catch { }
                _session = null;
            }

            if (_device != null)
            {
                try { _device.ConnectionStatusChanged -= OnConnectionStatusChanged; } catch { }
                foreach (var s in _services) { try { if (s.Service != null) s.Service.Dispose(); } catch { } }
                try { _device.Dispose(); } catch { }
                _device = null;
                _services.Clear();
                AppLog.Info("Disconnected.");
                RaiseConnection(false);
            }
        }

        private void RaiseConnection(bool connected)
        {
            var handler = ConnectionChanged;
            if (handler != null) handler(this, connected);
        }
    }
}
