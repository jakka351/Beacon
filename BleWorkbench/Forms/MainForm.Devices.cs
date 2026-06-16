using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using BleWorkbench.Ble;
using BleWorkbench.Core;
using BleWorkbench.Models;
using BleWorkbench.Transport;

namespace BleWorkbench.Forms
{
    public partial class MainForm
    {
        // ----------------------------------------------------------------------
        // Device list panel
        // ----------------------------------------------------------------------
        private void BuildDevicePanel(Control host)
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var header = new Panel { Dock = DockStyle.Top, Height = 28 };
            var title = new Label { Text = "Discovered Devices", Dock = DockStyle.Left, AutoSize = false, Width = 130, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) };
            var lblFilter = new Label { Text = "Filter:", Dock = DockStyle.Left, AutoSize = false, Width = 42, TextAlign = ContentAlignment.MiddleRight };
            _txtDeviceFilter = new TextBox { Dock = DockStyle.Fill };
            _txtDeviceFilter.TextChanged += (s, e) => RebuildDeviceView();
            header.Controls.Add(_txtDeviceFilter);
            header.Controls.Add(lblFilter);
            header.Controls.Add(title);

            _gridDevices = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            _gridDevices.DataSource = _deviceBinding;
            AddCol(_gridDevices, "AddressText", "Address", 130);
            AddCol(_gridDevices, "DisplayName", "Name", 150);
            AddCol(_gridDevices, "Rssi", "RSSI", 50);
            AddCol(_gridDevices, "AdvTypeText", "Adv Type", 115);
            AddCol(_gridDevices, "AddressTypeText", "Addr Kind", 130);
            AddCol(_gridDevices, "CompanyText", "Company", 150);
            AddCol(_gridDevices, "AdvCount", "Count", 55);
            AddCol(_gridDevices, "ConnectableText", "Conn", 48);
            AddCol(_gridDevices, "LastSeenText", "Last Seen", 75);
            _gridDevices.SelectionChanged += (s, e) => OnDeviceSelectionChanged();
            _gridDevices.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ConnectSelected(); };

            panel.Controls.Add(_gridDevices);
            panel.Controls.Add(header);
            host.Controls.Add(panel);
        }

        private static void AddCol(DataGridView grid, string prop, string header, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.True
            });
        }

        private void FlushDevices()
        {
            List<ulong> dirty;
            lock (_dirtyLock)
            {
                if (_dirtyDevices.Count == 0) return;
                dirty = new List<ulong>(_dirtyDevices);
                _dirtyDevices.Clear();
            }

            string filter = _txtDeviceFilter.Text;
            foreach (ulong addr in dirty)
            {
                BleDeviceInfo dev = _registry.Get(addr);
                if (dev == null) continue;
                bool match = MatchesDeviceFilter(dev, filter);
                int idx = _deviceBinding.IndexOf(dev);
                if (match)
                {
                    if (idx < 0) _deviceBinding.Add(dev);
                    else _deviceBinding.ResetItem(idx);
                }
                else if (idx >= 0)
                {
                    _deviceBinding.RemoveAt(idx);
                }
            }

            // Refresh the advertisement detail of the selected device if it changed.
            ulong? sel = SelectedDeviceAddress();
            if (sel.HasValue && dirty.Contains(sel.Value))
                UpdateAdvDetails(_registry.Get(sel.Value));
        }

        private void RebuildDeviceView()
        {
            _deviceBinding.RaiseListChangedEvents = false;
            _deviceBinding.Clear();
            string filter = _txtDeviceFilter.Text;
            foreach (var dev in _registry.Snapshot())
                if (MatchesDeviceFilter(dev, filter))
                    _deviceBinding.Add(dev);
            _deviceBinding.RaiseListChangedEvents = true;
            _deviceBinding.ResetBindings();
        }

        private static bool MatchesDeviceFilter(BleDeviceInfo dev, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            filter = filter.Trim();
            if (filter.Length == 0) return true;
            StringComparison c = StringComparison.OrdinalIgnoreCase;
            return dev.AddressText.IndexOf(filter, c) >= 0
                || (dev.Name != null && dev.Name.IndexOf(filter, c) >= 0)
                || dev.CompanyText.IndexOf(filter, c) >= 0
                || dev.ServicesText.IndexOf(filter, c) >= 0;
        }

        private void ResetDeviceView()
        {
            _deviceBinding.Clear();
            UpdateAdvDetails(null);
        }

        private ulong? SelectedDeviceAddress()
        {
            if (_gridDevices == null || _gridDevices.CurrentRow == null) return null;
            var dev = _gridDevices.CurrentRow.DataBoundItem as BleDeviceInfo;
            return dev != null ? (ulong?)dev.Address : null;
        }

        private BleDeviceInfo SelectedDevice()
        {
            ulong? a = SelectedDeviceAddress();
            return a.HasValue ? _registry.Get(a.Value) : null;
        }

        private void OnDeviceSelectionChanged()
        {
            UpdateAdvDetails(SelectedDevice());
            UpdateActionStates();
        }

        // ----------------------------------------------------------------------
        // Advertisement detail tab
        // ----------------------------------------------------------------------
        private TabPage BuildAdvTab()
        {
            var page = new TabPage("Advertisement");
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };

            _gridAdv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            AddCol(_gridAdv, "TypeCode", "AD Type", 70);
            AddCol(_gridAdv, "TypeName", "Description", 230);
            AddCol(_gridAdv, "Interpreted", "Interpreted", 340);
            AddCol(_gridAdv, "DataHex", "Raw (hex)", 360);

            var advHeader = new Label { Dock = DockStyle.Top, Height = 24, Text = "AD Structures", Font = new Font(Font, FontStyle.Bold), Padding = new Padding(4, 4, 0, 0) };
            split.Panel1.Controls.Add(_gridAdv);
            split.Panel1.Controls.Add(advHeader);

            _txtAdvDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9.5f),
                BackColor = SystemColors.Window
            };
            var detHeader = new Label { Dock = DockStyle.Top, Height = 24, Text = "Summary", Font = new Font(Font, FontStyle.Bold), Padding = new Padding(4, 4, 0, 0) };
            split.Panel2.Controls.Add(_txtAdvDetails);
            split.Panel2.Controls.Add(detHeader);

            page.Controls.Add(split);
            page.Enter += (s, e) => { try { split.SplitterDistance = Math.Max(150, split.Height / 2); } catch { } };
            return page;
        }

        private void UpdateAdvDetails(BleDeviceInfo dev)
        {
            if (_gridAdv == null) return;
            _gridAdv.DataSource = null;
            if (dev == null)
            {
                _txtAdvDetails.Text = "(no device selected)";
                return;
            }

            _gridAdv.DataSource = new List<AdvertisementSection>(dev.Sections);

            var sb = new StringBuilder();
            sb.AppendLine("Address      : " + dev.AddressText + "   (" + dev.AddressTypeText + ")");
            sb.AppendLine("Name         : " + dev.DisplayName);
            sb.AppendLine("RSSI         : " + dev.Rssi + " dBm");
            sb.AppendLine("Tx Power     : " + (string.IsNullOrEmpty(dev.TxPowerText) ? "n/a" : dev.TxPowerText));
            sb.AppendLine("Adv Type     : " + dev.AdvTypeText + (dev.Connectable ? "  (connectable)" : ""));
            sb.AppendLine("Adv Packets  : " + dev.AdvCount);
            sb.AppendLine("First / Last : " + dev.FirstSeen.ToString("HH:mm:ss") + "  /  " + dev.LastSeen.ToString("HH:mm:ss"));
            sb.AppendLine();

            if (dev.ServiceUuids.Count > 0)
            {
                sb.AppendLine("Service UUIDs:");
                foreach (var g in dev.ServiceUuids)
                    sb.AppendLine("  • " + AssignedNumbers.ServiceName(g));
                sb.AppendLine();
            }

            if (dev.ManufacturerData.Count > 0)
            {
                sb.AppendLine("Manufacturer Data:");
                foreach (var kv in dev.ManufacturerData)
                    sb.AppendLine("  • " + AssignedNumbers.CompanyName(kv.Key) + " : " + HexUtil.ToHex(kv.Value));
            }

            _txtAdvDetails.Text = sb.ToString();
        }

        // ----------------------------------------------------------------------
        // Scan
        // ----------------------------------------------------------------------
        private async void ToggleScan()
        {
            try
            {
                if (_scanner.IsScanning) { _scanner.Stop(); return; }

                // Diagnose the radio first so the user gets a clear reason if nothing will appear.
                RadioHealth h = await BleRadio.CheckAsync();
                if (!h.CanScan)
                {
                    AppLog.Warn(h.Summary);
                    if (!string.IsNullOrEmpty(h.Remediation)) AppLog.Warn("Fix: " + h.Remediation);
                    DialogResult r = MessageBox.Show(this,
                        h.Summary + "\r\n\r\n" + h.Remediation +
                        "\r\n\r\nStart the scanner anyway? (It will begin reporting devices as soon as a working radio is available.)",
                        "Bluetooth radio not ready", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) return;
                }

                _scanner.Start();
            }
            catch (Exception ex)
            {
                AppLog.Error("Scan toggle failed", ex);
                MessageBox.Show(this, "Could not start scanning.\r\n\r\n" + ex.Message +
                    "\r\n\r\nEnsure Bluetooth is turned on.", "Scan error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateScanButton(bool scanning)
        {
            _btnScan.Text = scanning ? "Stop Scan" : "Start Scan";
        }

        private void ClearDevices()
        {
            _registry.Clear();
            _deviceBinding.Clear();
            UpdateAdvDetails(null);
            AppLog.Info("Device list cleared.");
        }

        // ----------------------------------------------------------------------
        // Connect / GATT entry point
        // ----------------------------------------------------------------------
        private async void ConnectSelected()
        {
            ulong? addr = SelectedDeviceAddress();
            if (!addr.HasValue)
            {
                MessageBox.Show(this, "Select a device in the list first.", "Connect", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _btnConnect.Enabled = false;
            try
            {
                bool ok = await _gatt.ConnectAsync(addr.Value);
                if (ok)
                {
                    SelectTab("GATT Explorer");
                }
                else
                {
                    MessageBox.Show(this, "Could not connect to " + BleAddress.Format(addr.Value) +
                        ".\r\nThe device may be out of range, not connectable, or already paired with another host.",
                        "Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Connect failed", ex);
            }
            finally
            {
                UpdateActionStates();
            }
        }

        private void SelectTab(string text)
        {
            foreach (TabPage p in _tabs.TabPages)
                if (p.Text == text) { _tabs.SelectedTab = p; return; }
        }

        // ----------------------------------------------------------------------
        // Serial / USB transport helpers
        // ----------------------------------------------------------------------
        private void RefreshPorts()
        {
            object current = _cboPort.SelectedItem;
            _cboPort.Items.Clear();
            foreach (var p in PortEnumerator.ListSerialPorts())
                _cboPort.Items.Add(p);
            if (_cboPort.Items.Count > 0) _cboPort.SelectedIndex = 0;
            UpdateActionStates();
            AppLog.Info("Found " + _cboPort.Items.Count + " serial port(s).");
        }

        private void OpenSerial()
        {
            var info = _cboPort.SelectedItem as SerialPortInfo;
            if (info == null)
            {
                MessageBox.Show(this, "Select a COM port first.", "Open HCI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int baud;
            if (!int.TryParse(_cboBaud.SelectedItem as string, out baud)) baud = 115200;
            try
            {
                _serial.Open(info.Port, baud);
            }
            catch (Exception ex)
            {
                AppLog.Error("Open serial failed", ex);
                MessageBox.Show(this, "Could not open " + info.Port + ".\r\n\r\n" + ex.Message,
                    "Open HCI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StartSerialScan()
        {
            if (!_serial.IsOpen)
            {
                MessageBox.Show(this, "Open an external HCI adapter (COM port) first.", "Serial scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _serial.SendReset();
            _serial.SendLeSetScanParameters(true, 0x0010, 0x0010);
            _serial.SendLeScanEnable(true, false);
        }

        private void ShowUsbRadios()
        {
            var list = PortEnumerator.ListBluetoothRadios();
            var sb = new StringBuilder();
            if (list.Count == 0) sb.AppendLine("No USB / system Bluetooth radios reported by WMI.");
            foreach (var d in list)
            {
                sb.AppendLine("• " + d.Name);
                sb.AppendLine("    service=" + d.Service + "   id=" + d.DeviceId);
            }
            MessageBox.Show(this, sb.ToString(), "USB / System Bluetooth Radios", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
