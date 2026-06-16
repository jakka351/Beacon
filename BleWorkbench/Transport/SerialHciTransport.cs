using System;
using System.IO.Ports;
using System.Threading;
using BleWorkbench.Core;

namespace BleWorkbench.Transport
{
    /// <summary>
    /// Talks to an external Bluetooth controller / sniffer that exposes the HCI
    /// UART (H4) transport over a serial or USB-CDC COM port. Inbound bytes are
    /// reassembled and decoded into the shared device registry and packet log;
    /// standard LE scan commands can be issued to drive a controller into
    /// reporting advertisements.
    /// </summary>
    public class SerialHciTransport
    {
        private readonly HciFramer _framer = new HciFramer();
        private readonly HciDecoder _decoder;
        private SerialPort _port;
        private Thread _readThread;
        private volatile bool _running;

        public event EventHandler<bool> StateChanged;

        public bool IsOpen { get { return _port != null && _port.IsOpen; } }
        public string PortName { get; private set; }
        public int BaudRate { get; private set; }

        public SerialHciTransport(DeviceRegistry registry, PacketLog packets)
        {
            _decoder = new HciDecoder(registry, packets);
            _framer.FrameReady += (s, f) => { try { _decoder.Decode(f); } catch (Exception ex) { AppLog.Error("HCI decode", ex); } };
        }

        public void Open(string portName, int baud)
        {
            Close();
            _framer.Reset();
            _port = new SerialPort(portName, baud)
            {
                ReadTimeout = 250,
                WriteTimeout = 1000,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                ReadBufferSize = 1 << 16
            };
            _port.Open();
            PortName = portName;
            BaudRate = baud;
            _running = true;
            _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "HCI-Read" };
            _readThread.Start();
            AppLog.Success("Opened HCI transport on " + portName + " @ " + baud + " baud.");
            Raise(true);
        }

        public void Close()
        {
            _running = false;
            if (_readThread != null)
            {
                try { _readThread.Join(500); } catch { }
                _readThread = null;
            }
            if (_port != null)
            {
                try { if (_port.IsOpen) _port.Close(); } catch { }
                try { _port.Dispose(); } catch { }
                _port = null;
                AppLog.Info("Closed HCI transport on " + PortName + ".");
                Raise(false);
            }
        }

        private void ReadLoop()
        {
            var buffer = new byte[8192];
            while (_running && _port != null)
            {
                try
                {
                    int n = _port.Read(buffer, 0, buffer.Length);
                    if (n > 0) _framer.Feed(buffer, n);
                }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    if (_running) AppLog.Error("Serial read error", ex);
                    break;
                }
            }
        }

        #region HCI commands
        public void SendRaw(byte[] h4Frame)
        {
            if (!IsOpen) return;
            try
            {
                _port.Write(h4Frame, 0, h4Frame.Length);
                // Log the TX frame.
                var headerLen = h4Frame.Length >= 1 && h4Frame[0] == 0x01 ? 3 : 2;
                if (h4Frame.Length > headerLen)
                {
                    var payload = new byte[h4Frame.Length - 1];
                    Array.Copy(h4Frame, 1, payload, 0, payload.Length);
                    _decoder.Decode(new HciFrame
                    {
                        Type = (HciPacketType)h4Frame[0],
                        Payload = payload,
                        FullFrame = h4Frame
                    });
                }
            }
            catch (Exception ex) { AppLog.Error("Serial write error", ex); }
        }

        public void SendCommand(ushort opcode, byte[] parameters)
        {
            if (parameters == null) parameters = new byte[0];
            var f = new byte[4 + parameters.Length];
            f[0] = 0x01;
            f[1] = (byte)(opcode & 0xFF);
            f[2] = (byte)(opcode >> 8);
            f[3] = (byte)parameters.Length;
            Array.Copy(parameters, 0, f, 4, parameters.Length);
            SendRaw(f);
        }

        /// <summary>HCI_Reset (OGF=0x03, OCF=0x003).</summary>
        public void SendReset()
        {
            AppLog.Tx("HCI Reset");
            SendCommand(0x0C03, new byte[0]);
        }

        /// <summary>LE_Set_Scan_Parameters (0x200B).</summary>
        public void SendLeSetScanParameters(bool active, ushort interval, ushort window)
        {
            byte[] p =
            {
                (byte)(active ? 0x01 : 0x00),
                (byte)(interval & 0xFF), (byte)(interval >> 8),
                (byte)(window & 0xFF), (byte)(window >> 8),
                0x00, // own address type = public
                0x00  // filter policy = accept all
            };
            AppLog.Tx("LE Set Scan Parameters (" + (active ? "active" : "passive") + ")");
            SendCommand(0x200B, p);
        }

        /// <summary>LE_Set_Scan_Enable (0x200C).</summary>
        public void SendLeScanEnable(bool enable, bool filterDuplicates)
        {
            byte[] p = { (byte)(enable ? 0x01 : 0x00), (byte)(filterDuplicates ? 0x01 : 0x00) };
            AppLog.Tx("LE Set Scan Enable = " + enable);
            SendCommand(0x200C, p);
        }
        #endregion

        private void Raise(bool open)
        {
            var h = StateChanged;
            if (h != null) h(this, open);
        }
    }
}
