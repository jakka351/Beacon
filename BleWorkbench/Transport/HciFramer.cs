using System;
using System.Collections.Generic;

namespace BleWorkbench.Transport
{
    public enum HciPacketType : byte
    {
        Command = 0x01,
        AclData = 0x02,
        ScoData = 0x03,
        Event = 0x04,
        IsoData = 0x05
    }

    public class HciFrame
    {
        public HciPacketType Type { get; set; }
        public byte[] Payload { get; set; }
        /// <summary>Full H4 frame including the leading type byte (for capture/export).</summary>
        public byte[] FullFrame { get; set; }
    }

    /// <summary>
    /// Reassembles a UART (H4) byte stream into discrete HCI frames. Tolerant of
    /// partial reads and able to resynchronise after corruption by dropping a
    /// byte and retrying when an unknown packet-type indicator is seen.
    /// </summary>
    public class HciFramer
    {
        private readonly List<byte> _buf = new List<byte>(4096);
        public event EventHandler<HciFrame> FrameReady;

        public void Reset() { _buf.Clear(); }

        public void Feed(byte[] data, int count)
        {
            for (int i = 0; i < count; i++) _buf.Add(data[i]);
            Process();
        }

        private void Process()
        {
            while (_buf.Count > 0)
            {
                byte type = _buf[0];
                int headerLen;
                if (!HeaderLength(type, out headerLen)) { _buf.RemoveAt(0); continue; } // resync

                if (_buf.Count < 1 + headerLen) return; // need more header bytes

                int payloadLen = PayloadLength(type, headerLen);
                int total = 1 + headerLen + payloadLen;
                if (_buf.Count < total) return; // need more payload bytes

                var full = new byte[total];
                _buf.CopyTo(0, full, 0, total);
                _buf.RemoveRange(0, total);

                var payload = new byte[headerLen + payloadLen];
                Array.Copy(full, 1, payload, 0, payload.Length);

                var handler = FrameReady;
                if (handler != null)
                    handler(this, new HciFrame { Type = (HciPacketType)type, Payload = payload, FullFrame = full });
            }
        }

        private static bool HeaderLength(byte type, out int headerLen)
        {
            switch (type)
            {
                case 0x01: headerLen = 3; return true; // opcode(2) + plen(1)
                case 0x02: headerLen = 4; return true; // handle(2) + dlen(2)
                case 0x03: headerLen = 3; return true; // handle(2) + dlen(1)
                case 0x04: headerLen = 2; return true; // code(1) + plen(1)
                case 0x05: headerLen = 4; return true; // handle(2) + dlen(2)
                default: headerLen = 0; return false;
            }
        }

        private int PayloadLength(byte type, int headerLen)
        {
            switch (type)
            {
                case 0x01: return _buf[3];                       // plen at offset 3
                case 0x02: return _buf[3] | (_buf[4] << 8);      // dlen 16-bit LE
                case 0x03: return _buf[3];                       // dlen 8-bit
                case 0x04: return _buf[2];                       // plen at offset 2
                case 0x05: return (_buf[3] | (_buf[4] << 8)) & 0x3FFF; // 14-bit length
                default: return 0;
            }
        }
    }
}
