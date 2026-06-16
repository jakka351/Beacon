using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using BleWorkbench.Ble;
using BleWorkbench.Controls;
using BleWorkbench.Core;
using BleWorkbench.Models;
using BleWorkbench.Transport;

namespace BleWorkbench.Forms
{
    public partial class MainForm : Form
    {
        // ---- Subsystems -------------------------------------------------------
        private readonly PacketLog _packets = new PacketLog();
        private readonly DeviceRegistry _registry = new DeviceRegistry();
        private readonly BleScanner _scanner;
        private readonly GattClient _gatt;
        private readonly PeripheralServer _peripheral;
        private readonly SerialHciTransport _serial;

        // ---- Top-level chrome -------------------------------------------------
        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private StatusStrip _status;
        private SplitContainer _mainSplit;
        private TabControl _tabs;

        private ToolStripButton _btnScan;
        private ToolStripButton _btnConnect;
        private ToolStripButton _btnDisconnect;
        private ToolStripComboBox _cboPort;
        private ToolStripComboBox _cboBaud;
        private ToolStripButton _btnRefreshPorts;
        private ToolStripButton _btnOpenPort;
        private ToolStripButton _btnClosePort;
        private ToolStripButton _btnClearPackets;
        private ToolStripButton _btnExport;

        private ToolStripStatusLabel _stStatus;
        private ToolStripStatusLabel _stDevices;
        private ToolStripStatusLabel _stPackets;
        private ToolStripStatusLabel _stScan;
        private ToolStripStatusLabel _stTransport;
        private ToolStripStatusLabel _stConn;

        // ---- Device panel -----------------------------------------------------
        private DataGridView _gridDevices;
        private TextBox _txtDeviceFilter;
        private readonly BindingList<BleDeviceInfo> _deviceBinding = new BindingList<BleDeviceInfo>();
        private readonly Dictionary<ulong, int> _deviceRowByAddr = new Dictionary<ulong, int>();

        // ---- Advertisement tab ------------------------------------------------
        private DataGridView _gridAdv;
        private TextBox _txtAdvDetails;

        // ---- GATT tab ---------------------------------------------------------
        private TreeView _treeGatt;
        private Label _lblCharInfo;
        private HexView _hexGattValue;
        private TextBox _txtGattAscii;
        private TextBox _txtWriteData;
        private ComboBox _cboWriteFormat;
        private Button _btnRead;
        private Button _btnWrite;
        private Button _btnWriteNoResp;
        private Button _btnSubscribe;
        private Button _btnUnsubscribe;
        private DataGridView _gridNotifications;
        private readonly BindingList<NotificationRow> _notifyBinding = new BindingList<NotificationRow>();
        private GattCharacteristicView _selectedChar;

        // ---- Sniffer tab ------------------------------------------------------
        private DataGridView _gridPackets;
        private HexView _hexPacket;
        private TextBox _txtPacketFilter;
        private ComboBox _cboSourceFilter;
        private CheckBox _chkPause;
        private CheckBox _chkAutoScroll;
        private Label _lblPacketCount;
        private readonly List<BlePacket> _packetView = new List<BlePacket>();

        // ---- Emulator tab -----------------------------------------------------
        private TextBox _txtEmuName;
        private TextBox _txtEmuServiceUuid;
        private CheckBox _chkConnectable;
        private CheckBox _chkDiscoverable;
        private DataGridView _gridEmuChars;
        private readonly BindingList<CharacteristicDefinition> _emuChars = new BindingList<CharacteristicDefinition>();
        private TextBox _txtCharUuid;
        private CheckBox _chkCharRead, _chkCharWrite, _chkCharWriteNr, _chkCharNotify, _chkCharIndicate;
        private TextBox _txtCharInitial, _txtCharDesc;
        private TextBox _txtMfgCompany, _txtMfgData;
        private Button _btnStartPeripheral, _btnStartBroadcast, _btnStopEmu;
        private ComboBox _cboNotifyChar;
        private TextBox _txtNotifyValue;
        private Button _btnNotify;
        private Label _lblEmuStatus;

        // ---- Fuzzer tab -------------------------------------------------------
        private ComboBox _cboFuzzPattern;
        private NumericUpDown _numFuzzMin, _numFuzzMax, _numFuzzRate, _numFuzzCount;
        private CheckBox _chkFuzzNoResp;
        private Button _btnFuzzStart, _btnFuzzStop;
        private Label _lblFuzzStatus, _lblFuzzTarget;
        private volatile bool _fuzzRunning;

        // ---- Console tab ------------------------------------------------------
        private RichTextBox _rtbConsole;

        // ---- UI pump / state --------------------------------------------------
        private Timer _uiTimer;
        private readonly ConcurrentQueue<BlePacket> _pendingPackets = new ConcurrentQueue<BlePacket>();
        private readonly object _dirtyLock = new object();
        private readonly HashSet<ulong> _dirtyDevices = new HashSet<ulong>();
        private bool _fullScreen;
        private Rectangle _restoreBounds;
        private FormBorderStyle _restoreBorder;
        private FormWindowState _restoreState;

        public MainForm()
        {
            _scanner = new BleScanner(_packets, _registry);
            _gatt = new GattClient(_packets);
            _peripheral = new PeripheralServer(_packets);
            _serial = new SerialHciTransport(_registry, _packets);

            InitializeForm();
            BuildMenu();
            BuildToolbar();
            BuildStatusBar();
            BuildLayout();
            WireSubsystemEvents();
            StartPump();

            RefreshPorts();
            UpdateActionStates();
            AppLog.Info("Beacon — Bluetooth Low Energy Utility ready. Default adapter uses the Windows BLE stack; external adapters use Serial HCI.");
            if (AppLog.LogFilePath != null) AppLog.Info("Session log: " + AppLog.LogFilePath);
            CheckRadioAtStartup();
        }

        private async void CheckRadioAtStartup()
        {
            try
            {
                RadioHealth h = await BleRadio.CheckAsync();
                if (h.CanScan) { AppLog.Success(h.Summary); _stStatus.Text = h.Summary; }
                else
                {
                    AppLog.Warn(h.Summary);
                    if (!string.IsNullOrEmpty(h.Remediation)) AppLog.Warn("Fix: " + h.Remediation);
                    _stStatus.Text = "⚠ " + h.Summary;
                }
            }
            catch { }
        }

        private void InitializeForm()
        {
            Text = Branding.AppTitle;
            try { var ic = Branding.LoadIcon(); if (ic != null) Icon = ic; } catch { }
            Font = SystemFonts.MessageBoxFont; // default Segoe UI theme
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 700);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += OnKeyDown;
            FormClosing += OnFormClosing;
        }

        // ----------------------------------------------------------------------
        // Thread marshaling
        // ----------------------------------------------------------------------
        private void Ui(Action action)
        {
            if (IsDisposed || Disposing) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        // ----------------------------------------------------------------------
        // Menu / toolbar / status bar
        // ----------------------------------------------------------------------
        private void BuildMenu()
        {
            _menu = new MenuStrip { Dock = DockStyle.Top };

            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add(NewItem("Export packets to &CSV...", (s, e) => ExportPackets(ExportKind.Csv)));
            file.DropDownItems.Add(NewItem("Export packets to &Text...", (s, e) => ExportPackets(ExportKind.Text)));
            file.DropDownItems.Add(NewItem("Export to &BTSnoop (Wireshark)...", (s, e) => ExportPackets(ExportKind.BtSnoop)));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(NewItem("E&xit", (s, e) => Close()));
            _menu.Items.Add(file);

            var capture = new ToolStripMenuItem("&Capture");
            capture.DropDownItems.Add(NewItem("&Start / Stop Scan", (s, e) => ToggleScan(), Keys.F5));
            capture.DropDownItems.Add(NewItem("&Clear Devices", (s, e) => ClearDevices()));
            capture.DropDownItems.Add(NewItem("Clear &Packets", (s, e) => ClearPackets()));
            _menu.Items.Add(capture);

            var device = new ToolStripMenuItem("&Device");
            device.DropDownItems.Add(NewItem("&Connect", (s, e) => ConnectSelected(), Keys.F6));
            device.DropDownItems.Add(NewItem("&Disconnect", (s, e) => DisconnectGatt()));
            device.DropDownItems.Add(NewItem("Re-&discover services", (s, e) => RediscoverServices()));
            _menu.Items.Add(device);

            var emu = new ToolStripMenuItem("&Emulation");
            emu.DropDownItems.Add(NewItem("Start &Peripheral (GATT server)", (s, e) => StartPeripheral()));
            emu.DropDownItems.Add(NewItem("Start &Broadcast (advertiser)", (s, e) => StartBroadcast()));
            emu.DropDownItems.Add(NewItem("&Stop Emulation", (s, e) => StopEmulation()));
            _menu.Items.Add(emu);

            var tools = new ToolStripMenuItem("&Tools");
            tools.DropDownItems.Add(NewItem("&Refresh COM ports", (s, e) => RefreshPorts()));
            tools.DropDownItems.Add(NewItem("List &USB Bluetooth radios", (s, e) => ShowUsbRadios()));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add(NewItem("Send HCI &Reset (serial)", (s, e) => { _serial.SendReset(); }));
            tools.DropDownItems.Add(NewItem("Start LE scan on serial &adapter", (s, e) => StartSerialScan()));
            tools.DropDownItems.Add(NewItem("Stop LE scan on serial adapter", (s, e) => _serial.SendLeScanEnable(false, false)));
            _menu.Items.Add(tools);

            var view = new ToolStripMenuItem("&View");
            view.DropDownItems.Add(NewItem("&Full Screen", (s, e) => ToggleFullScreen(), Keys.F11));
            view.DropDownItems.Add(NewItem("Clear &Console", (s, e) => { if (_rtbConsole != null) _rtbConsole.Clear(); }));
            _menu.Items.Add(view);

            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add(NewItem("&About", (s, e) => ShowAbout()));
            _menu.Items.Add(help);

            MainMenuStrip = _menu;
            Controls.Add(_menu);
        }

        private static ToolStripMenuItem NewItem(string text, EventHandler handler, Keys shortcut = Keys.None)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += handler;
            if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
            return item;
        }

        private void BuildToolbar()
        {
            _toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(16, 16) };

            _btnScan = new ToolStripButton("Start Scan") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnScan.Click += (s, e) => ToggleScan();

            _btnConnect = new ToolStripButton("Connect") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnConnect.Click += (s, e) => ConnectSelected();

            _btnDisconnect = new ToolStripButton("Disconnect") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnDisconnect.Click += (s, e) => DisconnectGatt();

            _cboPort = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = 220 };
            _cboPort.SelectedIndexChanged += (s, e) => UpdateActionStates();
            _cboBaud = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = 90 };
            _cboBaud.Items.AddRange(new object[] { "115200", "1000000", "57600", "230400", "460800", "921600", "2000000", "3000000" });
            _cboBaud.SelectedIndex = 0;
            _btnRefreshPorts = new ToolStripButton("⟳") { ToolTipText = "Refresh COM ports", DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnRefreshPorts.Click += (s, e) => RefreshPorts();
            _btnOpenPort = new ToolStripButton("Open HCI") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnOpenPort.Click += (s, e) => OpenSerial();
            _btnClosePort = new ToolStripButton("Close HCI") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnClosePort.Click += (s, e) => _serial.Close();

            _btnClearPackets = new ToolStripButton("Clear Packets") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnClearPackets.Click += (s, e) => ClearPackets();
            _btnExport = new ToolStripButton("Export") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnExport.Click += (s, e) => ExportPackets(ExportKind.Csv);

            _toolbar.Items.Add(_btnScan);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(_btnConnect);
            _toolbar.Items.Add(_btnDisconnect);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(new ToolStripLabel("External adapter:"));
            _toolbar.Items.Add(_cboPort);
            _toolbar.Items.Add(_cboBaud);
            _toolbar.Items.Add(_btnRefreshPorts);
            _toolbar.Items.Add(_btnOpenPort);
            _toolbar.Items.Add(_btnClosePort);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(_btnClearPackets);
            _toolbar.Items.Add(_btnExport);

            Controls.Add(_toolbar);
        }

        private void BuildStatusBar()
        {
            _status = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = true };
            _stStatus = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _stDevices = new ToolStripStatusLabel("Devices: 0") { BorderSides = ToolStripStatusLabelBorderSides.Left };
            _stPackets = new ToolStripStatusLabel("Packets: 0") { BorderSides = ToolStripStatusLabelBorderSides.Left };
            _stScan = new ToolStripStatusLabel("Scan: off") { BorderSides = ToolStripStatusLabelBorderSides.Left };
            _stTransport = new ToolStripStatusLabel("HCI: closed") { BorderSides = ToolStripStatusLabelBorderSides.Left };
            _stConn = new ToolStripStatusLabel("GATT: disconnected") { BorderSides = ToolStripStatusLabelBorderSides.Left };

            _status.Items.AddRange(new ToolStripItem[] { _stStatus, _stDevices, _stPackets, _stScan, _stTransport, _stConn });
            Controls.Add(_status);
        }

        // ----------------------------------------------------------------------
        // Layout: device list on the left, tabbed workspace on the right
        // ----------------------------------------------------------------------
        private void BuildLayout()
        {
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                FixedPanel = FixedPanel.Panel1
            };

            BuildDevicePanel(_mainSplit.Panel1);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(BuildAdvTab());
            _tabs.TabPages.Add(BuildGattTab());
            _tabs.TabPages.Add(BuildSnifferTab());
            _tabs.TabPages.Add(BuildEmulatorTab());
            _tabs.TabPages.Add(BuildFuzzerTab());
            _tabs.TabPages.Add(BuildConsoleTab());
            _mainSplit.Panel2.Controls.Add(_tabs);

            Controls.Add(_mainSplit);
            // SplitContainer must sit below the docked menu/toolbar but above the status strip.
            _mainSplit.BringToFront();

            Shown += (s, e) =>
            {
                try { _mainSplit.SplitterDistance = 520; } catch { }
            };
        }

        // ----------------------------------------------------------------------
        // Subsystem event wiring (all marshaled to UI thread)
        // ----------------------------------------------------------------------
        private void WireSubsystemEvents()
        {
            AppLog.Logged += (s, entry) => Ui(() => AppendConsole(entry));

            _packets.PacketAdded += (s, p) => _pendingPackets.Enqueue(p);
            _packets.Cleared += (s, e) => Ui(ResetPacketView);

            _registry.Updated += (s, dev) => { lock (_dirtyLock) _dirtyDevices.Add(dev.Address); };
            _registry.Cleared += (s, e) => Ui(ResetDeviceView);

            _scanner.ScanStateChanged += (s, on) => Ui(() => { UpdateScanButton(on); UpdateStatus(); });

            _gatt.ConnectionChanged += (s, connected) => Ui(() => { UpdateActionStates(); UpdateStatus(); });
            _gatt.ServicesDiscovered += (s, e) => Ui(PopulateGattTree);
            _gatt.Notified += (s, n) => Ui(() => OnGattNotification(n));

            _peripheral.StateChanged += (s, role) => Ui(() => { UpdateEmulatorStatus(role); UpdateStatus(); });
            _peripheral.Activity += (s, msg) => Ui(() => { if (_lblEmuStatus != null) _lblEmuStatus.Text = msg; });

            _serial.StateChanged += (s, open) => Ui(() => { UpdateActionStates(); UpdateStatus(); });
        }

        // ----------------------------------------------------------------------
        // UI pump: drain queued packets and dirty devices at ~12 Hz
        // ----------------------------------------------------------------------
        private void StartPump()
        {
            _uiTimer = new Timer { Interval = 80 };
            _uiTimer.Tick += (s, e) => PumpTick();
            _uiTimer.Start();
        }

        private void PumpTick()
        {
            FlushDevices();
            FlushPackets();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_stDevices == null) return;
            _stDevices.Text = "Devices: " + _registry.Count;
            _stPackets.Text = "Packets: " + _packets.Count;
            _stScan.Text = _scanner.IsScanning ? "Scan: ON" : "Scan: off";
            _stTransport.Text = _serial.IsOpen ? ("HCI: " + _serial.PortName) : "HCI: closed";
            _stConn.Text = _gatt.IsConnected ? ("GATT: " + (string.IsNullOrEmpty(_gatt.DeviceName) ? _gatt.AddressText : _gatt.DeviceName)) : "GATT: disconnected";
        }

        private void UpdateActionStates()
        {
            bool deviceSelected = SelectedDeviceAddress().HasValue;
            _btnConnect.Enabled = deviceSelected && !_gatt.IsConnected;
            _btnDisconnect.Enabled = _gatt.IsConnected;
            _btnOpenPort.Enabled = !_serial.IsOpen && _cboPort.ComboBox != null && _cboPort.SelectedIndex >= 0;
            _btnClosePort.Enabled = _serial.IsOpen;
            if (_lblFuzzTarget != null)
                _lblFuzzTarget.Text = "Target: " + (_selectedChar != null ? AssignedNumbers.CharacteristicName(_selectedChar.Uuid) : "(select a writable characteristic in GATT Explorer)");
        }

        // ----------------------------------------------------------------------
        // Keyboard / full-screen / lifecycle
        // ----------------------------------------------------------------------
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11) { ToggleFullScreen(); e.Handled = true; }
        }

        private void ToggleFullScreen()
        {
            if (!_fullScreen)
            {
                _restoreBounds = Bounds;
                _restoreBorder = FormBorderStyle;
                _restoreState = WindowState;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
                _menu.Visible = false;
                _fullScreen = true;
                _stStatus.Text = "Full screen — press F11 to exit";
            }
            else
            {
                FormBorderStyle = _restoreBorder;
                WindowState = _restoreState;
                Bounds = _restoreBounds;
                _menu.Visible = true;
                _fullScreen = false;
                _stStatus.Text = "Ready";
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            try { _uiTimer.Stop(); } catch { }
            _fuzzRunning = false;
            try { _scanner.Stop(); } catch { }
            try { _gatt.Disconnect(); } catch { }
            try { _peripheral.Stop(); } catch { }
            try { _serial.Close(); } catch { }
        }

        private void ShowAbout()
        {
            using (var about = new AboutForm())
                about.ShowDialog(this);
        }

        private enum ExportKind { Csv, Text, BtSnoop }
    }

    /// <summary>Row model for the GATT notification/value history grid.</summary>
    public class NotificationRow
    {
        public string Time { get; set; }
        public string Characteristic { get; set; }
        public int Length { get; set; }
        public string Hex { get; set; }
        public string Ascii { get; set; }
    }
}
