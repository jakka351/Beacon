using System;
using System.Collections.Generic;
using BleWorkbench.Models;

namespace BleWorkbench.Core
{
    /// <summary>
    /// Single source of truth for discovered devices, shared by every capture
    /// source (WinRT scanner, external HCI sniffer). Producers update entries on
    /// background threads; the UI subscribes to <see cref="Updated"/> and marshals.
    /// </summary>
    public class DeviceRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<ulong, BleDeviceInfo> _devices = new Dictionary<ulong, BleDeviceInfo>();

        public event EventHandler<BleDeviceInfo> Updated;
        public event EventHandler Cleared;

        public int Count { get { lock (_gate) return _devices.Count; } }

        public BleDeviceInfo GetOrAdd(ulong address)
        {
            lock (_gate)
            {
                BleDeviceInfo d;
                if (!_devices.TryGetValue(address, out d))
                {
                    d = new BleDeviceInfo { Address = address, FirstSeen = DateTime.Now };
                    _devices[address] = d;
                }
                return d;
            }
        }

        public BleDeviceInfo Get(ulong address)
        {
            lock (_gate)
            {
                BleDeviceInfo d;
                return _devices.TryGetValue(address, out d) ? d : null;
            }
        }

        public List<BleDeviceInfo> Snapshot()
        {
            lock (_gate) return new List<BleDeviceInfo>(_devices.Values);
        }

        public void RaiseUpdated(BleDeviceInfo device)
        {
            var h = Updated;
            if (h != null) h(this, device);
        }

        public void Clear()
        {
            lock (_gate) _devices.Clear();
            var h = Cleared;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
