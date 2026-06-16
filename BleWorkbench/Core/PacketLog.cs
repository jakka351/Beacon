using System;
using System.Collections.Generic;
using System.Threading;
using BleWorkbench.Models;

namespace BleWorkbench.Core
{
    /// <summary>
    /// Central, thread-safe sink for every captured packet. Producers (the
    /// scanner, GATT client, peripheral server and serial HCI transport) call
    /// <see cref="Add"/> from background threads; the UI subscribes to
    /// <see cref="PacketAdded"/> and marshals to the UI thread itself.
    /// </summary>
    public class PacketLog
    {
        private readonly object _gate = new object();
        private readonly List<BlePacket> _packets = new List<BlePacket>();
        private long _counter;
        private int _capacity = 200000;

        public event EventHandler<BlePacket> PacketAdded;
        public event EventHandler Cleared;

        /// <summary>Hard cap on retained packets; oldest are dropped beyond this.</summary>
        public int Capacity
        {
            get { return _capacity; }
            set { _capacity = Math.Max(1000, value); }
        }

        public int Count
        {
            get { lock (_gate) return _packets.Count; }
        }

        public BlePacket Add(BlePacket p)
        {
            if (p == null) return null;
            lock (_gate)
            {
                p.Index = Interlocked.Increment(ref _counter);
                _packets.Add(p);
                if (_packets.Count > _capacity)
                    _packets.RemoveRange(0, _packets.Count - _capacity);
            }
            var handler = PacketAdded;
            if (handler != null) handler(this, p);
            return p;
        }

        public List<BlePacket> Snapshot()
        {
            lock (_gate) return new List<BlePacket>(_packets);
        }

        public void Clear()
        {
            lock (_gate) _packets.Clear();
            var handler = Cleared;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
