using System;
using System.Collections.Generic;
using System.IO;
using BleWorkbench.Core;
using BleWorkbench.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace BleWorkbench.Ble
{
    /// <summary>
    /// Wraps <see cref="BluetoothLEAdvertisementWatcher"/>. Each received report
    /// is aggregated into a <see cref="BleDeviceInfo"/> (keyed by address) and
    /// recorded as a <see cref="BlePacket"/> in the shared packet log.
    /// Events are raised on WinRT threads; subscribers must marshal to the UI.
    /// </summary>
    public class BleScanner
    {
        private readonly PacketLog _packets;
        private readonly DeviceRegistry _registry;
        private BluetoothLEAdvertisementWatcher _watcher;

        public event EventHandler<bool> ScanStateChanged;

        public bool ActiveScan { get; set; }
        public bool AllowExtended { get; set; }
        public bool IsScanning { get; private set; }

        public BleScanner(PacketLog packets, DeviceRegistry registry)
        {
            _packets = packets;
            _registry = registry;
            ActiveScan = true;
        }

        public void Start()
        {
            if (IsScanning) return;
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = ActiveScan ? BluetoothLEScanningMode.Active : BluetoothLEScanningMode.Passive
            };
            try { _watcher.AllowExtendedAdvertisements = AllowExtended; } catch { }

            _watcher.Received += OnReceived;
            _watcher.Stopped += OnStopped;
            _watcher.Start();
            IsScanning = true;
            AppLog.Success("Scanner started (" + (ActiveScan ? "active" : "passive") +
                           (AllowExtended ? ", extended" : "") + ").");
            RaiseState(true);
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                try { _watcher.Stop(); } catch { }
                _watcher.Received -= OnReceived;
                _watcher.Stopped -= OnStopped;
                _watcher = null;
            }
            if (IsScanning)
            {
                IsScanning = false;
                AppLog.Info("Scanner stopped.");
                RaiseState(false);
            }
        }

        private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            IsScanning = false;
            AppLog.Warn("Scanner stopped by system: " + args.Error);
            RaiseState(false);
        }

        private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try
            {
                ulong addr = args.BluetoothAddress;
                BleDeviceInfo info = _registry.GetOrAdd(addr);

                info.LastSeen = DateTime.Now;
                info.AdvCount++;
                info.Rssi = args.RawSignalStrengthInDBm;
                info.LastAdvType = MapType(args.AdvertisementType);
                info.Connectable = IsConnectable(args.AdvertisementType);
                info.AddressKind = MapAddressKind(args.BluetoothAddressType);

                try
                {
                    var tx = args.TransmitPowerLevelInDBm;
                    if (tx.HasValue) info.TxPower = (short)tx.Value;
                }
                catch { }

                var adv = args.Advertisement;
                if (adv != null)
                {
                    if (!string.IsNullOrEmpty(adv.LocalName)) info.Name = adv.LocalName;

                    info.ServiceUuids.Clear();
                    foreach (Guid g in adv.ServiceUuids) info.ServiceUuids.Add(g);

                    info.ManufacturerData.Clear();
                    foreach (var md in adv.ManufacturerData)
                        info.ManufacturerData[md.CompanyId] = BleBuffers.ToBytes(md.Data);

                    info.Sections.Clear();
                    foreach (var ds in adv.DataSections)
                        info.Sections.Add(new AdvertisementSection { DataType = ds.DataType, Data = BleBuffers.ToBytes(ds.Data) });
                }

                RecordPacket(info, args);
                _registry.RaiseUpdated(info);
            }
            catch (Exception ex)
            {
                AppLog.Error("Advertisement processing failed", ex);
            }
        }

        private void RecordPacket(BleDeviceInfo info, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (_packets == null) return;

            byte[] raw = SerializeSections(info.Sections);
            var p = new BlePacket
            {
                Timestamp = DateTime.Now,
                Source = PacketSource.WinRtAdvertisement,
                Direction = PacketDirection.Rx,
                Address = info.AddressText,
                PacketType = TypeName(info.LastAdvType),
                Rssi = info.Rssi,
                Raw = raw,
                Summary = BuildSummary(info)
            };
            _packets.Add(p);
        }

        private static string BuildSummary(BleDeviceInfo info)
        {
            string name = string.IsNullOrEmpty(info.Name) ? "(no name)" : info.Name;
            string svc = info.ServiceUuids.Count > 0 ? "  svc=" + info.ServiceUuids.Count : "";
            string mfg = info.ManufacturerData.Count > 0 ? "  mfg=" + info.CompanyText : "";
            return name + svc + mfg;
        }

        /// <summary>Reassembles AD structures into the on-air payload byte order: [len][type][data]...</summary>
        private static byte[] SerializeSections(List<AdvertisementSection> sections)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var s in sections)
                {
                    int len = 1 + (s.Data == null ? 0 : s.Data.Length);
                    if (len > 255) len = 255;
                    ms.WriteByte((byte)len);
                    ms.WriteByte(s.DataType);
                    if (s.Data != null && s.Data.Length > 0) ms.Write(s.Data, 0, s.Data.Length);
                }
                return ms.ToArray();
            }
        }

        private static AdvType MapType(BluetoothLEAdvertisementType t)
        {
            switch (t)
            {
                case BluetoothLEAdvertisementType.ConnectableUndirected: return AdvType.ConnectableUndirected;
                case BluetoothLEAdvertisementType.ConnectableDirected: return AdvType.ConnectableDirected;
                case BluetoothLEAdvertisementType.ScannableUndirected: return AdvType.ScannableUndirected;
                case BluetoothLEAdvertisementType.NonConnectableUndirected: return AdvType.NonConnectableUndirected;
                case BluetoothLEAdvertisementType.ScanResponse: return AdvType.ScanResponse;
                default: return AdvType.Extended;
            }
        }

        private static bool IsConnectable(BluetoothLEAdvertisementType t)
        {
            return t == BluetoothLEAdvertisementType.ConnectableUndirected ||
                   t == BluetoothLEAdvertisementType.ConnectableDirected;
        }

        private static AddressKind MapAddressKind(BluetoothAddressType t)
        {
            switch (t)
            {
                case BluetoothAddressType.Public: return AddressKind.Public;
                case BluetoothAddressType.Random: return AddressKind.Random;
                default: return AddressKind.Unspecified;
            }
        }

        private static string TypeName(AdvType t)
        {
            switch (t)
            {
                case AdvType.ConnectableUndirected: return "ADV_IND";
                case AdvType.ConnectableDirected: return "ADV_DIRECT_IND";
                case AdvType.ScannableUndirected: return "ADV_SCAN_IND";
                case AdvType.NonConnectableUndirected: return "ADV_NONCONN_IND";
                case AdvType.ScanResponse: return "SCAN_RSP";
                case AdvType.Extended: return "ADV_EXT_IND";
                default: return "ADV";
            }
        }

        private void RaiseState(bool started)
        {
            var handler = ScanStateChanged;
            if (handler != null) handler(this, started);
        }
    }
}
