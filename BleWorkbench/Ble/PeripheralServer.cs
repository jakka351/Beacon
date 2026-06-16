using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BleWorkbench.Core;
using BleWorkbench.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleWorkbench.Ble
{
    /// <summary>
    /// Peripheral / broadcaster emulation. Two modes:
    ///  * Broadcast-only via <see cref="BluetoothLEAdvertisementPublisher"/>.
    ///  * Connectable GATT server via <see cref="GattServiceProvider"/> with
    ///    live read / write / notify handling.
    /// </summary>
    public class PeripheralServer
    {
        private readonly PacketLog _packets;
        private GattServiceProvider _provider;
        private BluetoothLEAdvertisementPublisher _publisher;

        private readonly Dictionary<Guid, GattLocalCharacteristic> _chars = new Dictionary<Guid, GattLocalCharacteristic>();
        private readonly Dictionary<Guid, byte[]> _values = new Dictionary<Guid, byte[]>();
        private readonly object _gate = new object();

        public EmulatorRole Role { get; private set; }
        public bool IsRunning { get { return Role != EmulatorRole.Off; } }
        public PeripheralDefinition CurrentDefinition { get; private set; }

        public event EventHandler<EmulatorRole> StateChanged;
        public event EventHandler<string> Activity;

        public PeripheralServer(PacketLog packets) { _packets = packets; }

        public IEnumerable<Guid> CharacteristicUuids { get { lock (_gate) return new List<Guid>(_chars.Keys); } }

        public byte[] GetValue(Guid uuid)
        {
            lock (_gate)
            {
                byte[] v;
                return _values.TryGetValue(uuid, out v) ? v : new byte[0];
            }
        }

        public void SetValue(Guid uuid, byte[] data)
        {
            lock (_gate) _values[uuid] = data ?? new byte[0];
        }

        #region broadcast-only
        public bool StartBroadcast(AdvertisementDefinition def)
        {
            Stop();

            // Windows reserves the GAP fields (Flags, Local Name, Service UUIDs) of an
            // advertisement publisher payload. Some builds accept them, others reject the
            // whole payload at Start() with "value out of range". Try the full payload, then
            // gracefully fall back to a manufacturer-data-only advertisement.
            if (TryStartPublisher(def, true))
            {
                Role = EmulatorRole.BroadcasterOnly;
                AppLog.Success("Broadcasting started (full payload: name / service UUID / manufacturer data).");
                RaiseState();
                return true;
            }
            if (TryStartPublisher(def, false))
            {
                Role = EmulatorRole.BroadcasterOnly;
                AppLog.Warn("Windows rejected the name/service-UUID fields for the publisher; broadcasting manufacturer data only. " +
                            "Use Peripheral mode to advertise a name and service UUID.");
                RaiseState();
                return true;
            }
            AppLog.Error("Could not start broadcasting on this adapter.");
            return false;
        }

        private bool TryStartPublisher(AdvertisementDefinition def, bool includeGapFields)
        {
            try
            {
                var pub = new BluetoothLEAdvertisementPublisher();
                var adv = pub.Advertisement;

                if (includeGapFields)
                {
                    if (!string.IsNullOrEmpty(def.LocalName)) adv.LocalName = def.LocalName;
                    foreach (Guid g in def.ServiceUuids) adv.ServiceUuids.Add(g);
                }

                // Always include a manufacturer-data section so there is a valid payload.
                ushort company = def.CompanyId ?? 0xFFFF;
                adv.ManufacturerData.Add(new BluetoothLEManufacturerData(company, BleBuffers.FromBytes(def.ManufacturerData ?? new byte[0])));

                foreach (var s in def.ExtraSections)
                    adv.DataSections.Add(new BluetoothLEAdvertisementDataSection(s.Key, BleBuffers.FromBytes(s.Value)));

                pub.StatusChanged += OnPublisherStatusChanged;
                pub.Start();
                _publisher = pub;
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Warn("Publisher start attempt (" + (includeGapFields ? "full payload" : "manufacturer-data only") + ") failed: " + ex.Message);
                return false;
            }
        }

        private void OnPublisherStatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
        {
            AppLog.Info("Publisher status: " + args.Status + (args.Error != BluetoothError.Success ? " (" + args.Error + ")" : ""));
            Raise("Publisher: " + args.Status);
        }
        #endregion

        #region peripheral (GATT server)
        public async Task<bool> StartPeripheralAsync(PeripheralDefinition def)
        {
            Stop();
            CurrentDefinition = def;

            GattServiceProviderResult result = await GattServiceProvider.CreateAsync(def.ServiceUuid);
            if (result.Error != BluetoothError.Success)
            {
                AppLog.Error("GATT service provider creation failed: " + result.Error);
                return false;
            }
            _provider = result.ServiceProvider;

            lock (_gate) { _chars.Clear(); _values.Clear(); }

            foreach (CharacteristicDefinition cd in def.Characteristics)
            {
                if (!await CreateCharacteristicAsync(cd)) return false;
            }

            _provider.AdvertisementStatusChanged += OnProviderStatusChanged;

            var adParams = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = def.Connectable,
                IsDiscoverable = def.Discoverable
            };
            _provider.StartAdvertising(adParams);

            Role = EmulatorRole.Peripheral;
            AppLog.Success("Peripheral started: service " + AssignedNumbers.ServiceName(def.ServiceUuid) +
                           " with " + def.Characteristics.Count + " characteristic(s).");
            RaiseState();
            return true;
        }

        private async Task<bool> CreateCharacteristicAsync(CharacteristicDefinition cd)
        {
            GattCharacteristicProperties props = 0;
            if (cd.Read) props |= GattCharacteristicProperties.Read;
            if (cd.Write) props |= GattCharacteristicProperties.Write;
            if (cd.WriteNoResponse) props |= GattCharacteristicProperties.WriteWithoutResponse;
            if (cd.Notify) props |= GattCharacteristicProperties.Notify;
            if (cd.Indicate) props |= GattCharacteristicProperties.Indicate;

            var p = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = props,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                WriteProtectionLevel = GattProtectionLevel.Plain
            };
            if (!string.IsNullOrEmpty(cd.UserDescription)) p.UserDescription = cd.UserDescription;
            if (cd.Read && cd.InitialValue != null) p.StaticValue = BleBuffers.FromBytes(cd.InitialValue);

            GattLocalCharacteristicResult cr = await _provider.Service.CreateCharacteristicAsync(cd.Uuid, p);
            if (cr.Error != BluetoothError.Success)
            {
                AppLog.Error("Characteristic " + AssignedNumbers.CharacteristicName(cd.Uuid) + " failed: " + cr.Error);
                return false;
            }

            GattLocalCharacteristic ch = cr.Characteristic;
            lock (_gate)
            {
                _chars[cd.Uuid] = ch;
                _values[cd.Uuid] = cd.InitialValue ?? new byte[0];
            }

            if (cd.Read) ch.ReadRequested += OnReadRequested;
            if (cd.Write || cd.WriteNoResponse) ch.WriteRequested += OnWriteRequested;
            if (cd.Notify || cd.Indicate) ch.SubscribedClientsChanged += OnSubscribedClientsChanged;
            return true;
        }

        private async void OnReadRequested(GattLocalCharacteristic sender, GattReadRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                GattReadRequest request = await args.GetRequestAsync();
                if (request == null) return;
                byte[] value = GetValue(sender.Uuid);
                request.RespondWithValue(BleBuffers.FromBytes(value));
                Record(PacketDirection.Tx, "Read Rsp", sender.Uuid, value, "Central read our value");
                AppLog.Tx("[Peripheral] Read " + AssignedNumbers.CharacteristicName(sender.Uuid) + " -> " + HexUtil.ToHex(value));
            }
            catch (Exception ex) { AppLog.Error("Peripheral read handler", ex); }
            finally { deferral.Complete(); }
        }

        private async void OnWriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                GattWriteRequest request = await args.GetRequestAsync();
                if (request == null) return;
                byte[] data = BleBuffers.ToBytes(request.Value);
                SetValue(sender.Uuid, data);
                Record(PacketDirection.Rx, request.Option == GattWriteOption.WriteWithResponse ? "Write Req" : "Write Cmd",
                    sender.Uuid, data, "Central wrote " + data.Length + " bytes");
                AppLog.Rx("[Peripheral] Write " + AssignedNumbers.CharacteristicName(sender.Uuid) + " <- " + HexUtil.ToHex(data));
                if (request.Option == GattWriteOption.WriteWithResponse) request.Respond();
                Raise("Write to " + AssignedNumbers.CharacteristicName(sender.Uuid));
            }
            catch (Exception ex) { AppLog.Error("Peripheral write handler", ex); }
            finally { deferral.Complete(); }
        }

        private void OnSubscribedClientsChanged(GattLocalCharacteristic sender, object args)
        {
            int n = sender.SubscribedClients.Count;
            AppLog.Info("[Peripheral] " + AssignedNumbers.CharacteristicName(sender.Uuid) + " subscribers: " + n);
            Raise(AssignedNumbers.CharacteristicName(sender.Uuid) + " subscribers: " + n);
        }

        public async Task<bool> NotifyAsync(Guid uuid, byte[] data)
        {
            GattLocalCharacteristic ch;
            lock (_gate) { if (!_chars.TryGetValue(uuid, out ch)) return false; }
            SetValue(uuid, data);
            try
            {
                await ch.NotifyValueAsync(BleBuffers.FromBytes(data));
                Record(PacketDirection.Tx, "Notify", uuid, data, "Notified subscribers");
                AppLog.Tx("[Peripheral] Notify " + AssignedNumbers.CharacteristicName(uuid) + " -> " + HexUtil.ToHex(data));
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Notify failed", ex);
                return false;
            }
        }

        private void OnProviderStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            AppLog.Info("Service provider advertising: " + args.Status +
                        (args.Error != BluetoothError.Success ? " (" + args.Error + ")" : ""));
            Raise("Advertising: " + args.Status);
        }
        #endregion

        private void Record(PacketDirection dir, string type, Guid uuid, byte[] data, string summary)
        {
            if (_packets == null) return;
            _packets.Add(new BlePacket
            {
                Source = PacketSource.WinRtPeripheral,
                Direction = dir,
                Address = "(local)",
                PacketType = type,
                Raw = data ?? new byte[0],
                Summary = AssignedNumbers.CharacteristicName(uuid) + "  " + summary
            });
        }

        public void Stop()
        {
            if (_provider != null)
            {
                try { _provider.StopAdvertising(); } catch { }
                try { _provider.AdvertisementStatusChanged -= OnProviderStatusChanged; } catch { }
                _provider = null;
            }
            if (_publisher != null)
            {
                try { _publisher.Stop(); } catch { }
                try { _publisher.StatusChanged -= OnPublisherStatusChanged; } catch { }
                _publisher = null;
            }
            lock (_gate) { _chars.Clear(); }
            if (Role != EmulatorRole.Off)
            {
                Role = EmulatorRole.Off;
                AppLog.Info("Emulation stopped.");
                RaiseState();
            }
        }

        private void RaiseState()
        {
            var h = StateChanged;
            if (h != null) h(this, Role);
        }

        private void Raise(string msg)
        {
            var h = Activity;
            if (h != null) h(this, msg);
        }
    }
}
