using System;
using System.Drawing;
using System.Windows.Forms;
using BleWorkbench.Ble;
using BleWorkbench.Controls;
using BleWorkbench.Core;

namespace BleWorkbench.Forms
{
    public partial class MainForm
    {
        private TabPage BuildGattTab()
        {
            var page = new TabPage("GATT Explorer");
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };

            // Left: attribute tree
            _treeGatt = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                FullRowSelect = true,
                ShowLines = true,
                Font = new Font("Segoe UI", 9f)
            };
            _treeGatt.AfterSelect += (s, e) => OnGattNodeSelected(e.Node);
            var treeHeader = new Label { Dock = DockStyle.Top, Height = 24, Text = "Attribute Table", Font = new Font(Font, FontStyle.Bold), Padding = new Padding(4, 4, 0, 0) };
            split.Panel1.Controls.Add(_treeGatt);
            split.Panel1.Controls.Add(treeHeader);

            // Right: detail / operations
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _lblCharInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Select a characteristic to read, write or subscribe.",
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            table.Controls.Add(_lblCharInfo, 0, 0);

            table.Controls.Add(BuildGattValueGroup(), 0, 1);
            table.Controls.Add(BuildGattWriteGroup(), 0, 2);
            table.Controls.Add(BuildGattNotifyGroup(), 0, 3);

            split.Panel2.Controls.Add(table);

            page.Controls.Add(split);
            page.Enter += (s, e) => { try { split.SplitterDistance = Math.Max(260, split.Width * 2 / 5); } catch { } };
            return page;
        }

        private GroupBox BuildGattValueGroup()
        {
            var grp = new GroupBox { Text = "Value", Dock = DockStyle.Fill };
            _hexGattValue = new HexView { Dock = DockStyle.Fill };
            _txtGattAscii = new TextBox { Dock = DockStyle.Bottom, ReadOnly = true, Font = new Font("Consolas", 9.5f), BackColor = SystemColors.Window };
            var asciiLabel = new Label { Dock = DockStyle.Bottom, Height = 18, Text = "ASCII:", Padding = new Padding(2, 2, 0, 0) };
            grp.Controls.Add(_hexGattValue);
            grp.Controls.Add(asciiLabel);
            grp.Controls.Add(_txtGattAscii);
            return grp;
        }

        private GroupBox BuildGattWriteGroup()
        {
            var grp = new GroupBox { Text = "Read / Write / Subscribe", Dock = DockStyle.Fill };

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _cboWriteFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            _cboWriteFormat.Items.AddRange(new object[] { "Auto-detect", "Hex bytes", "Text (UTF-8)", "Decimal CSV" });
            _cboWriteFormat.SelectedIndex = 0;
            _btnRead = new Button { Text = "Read", Width = 70 };
            _btnWrite = new Button { Text = "Write", Width = 70 };
            _btnWriteNoResp = new Button { Text = "Write (No Resp)", Width = 110 };
            _btnSubscribe = new Button { Text = "Subscribe", Width = 90 };
            _btnUnsubscribe = new Button { Text = "Unsubscribe", Width = 90 };
            _btnRead.Click += (s, e) => DoRead();
            _btnWrite.Click += (s, e) => DoWrite(true);
            _btnWriteNoResp.Click += (s, e) => DoWrite(false);
            _btnSubscribe.Click += (s, e) => DoSubscribe();
            _btnUnsubscribe.Click += (s, e) => DoUnsubscribe();
            bar.Controls.Add(new Label { Text = "Format:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            bar.Controls.Add(_cboWriteFormat);
            bar.Controls.Add(_btnRead);
            bar.Controls.Add(_btnWrite);
            bar.Controls.Add(_btnWriteNoResp);
            bar.Controls.Add(_btnSubscribe);
            bar.Controls.Add(_btnUnsubscribe);

            _txtWriteData = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = ScrollBars.Vertical
            };
            var hint = new Label { Dock = DockStyle.Bottom, Height = 18, ForeColor = SystemColors.GrayText, Text = "Examples:  Hello   |   01 A2 FF   |   0x41,0x42   |   1,2,3" };

            grp.Controls.Add(_txtWriteData);
            grp.Controls.Add(hint);
            grp.Controls.Add(bar);
            return grp;
        }

        private GroupBox BuildGattNotifyGroup()
        {
            var grp = new GroupBox { Text = "Notifications / Value History", Dock = DockStyle.Fill };
            _gridNotifications = new DataGridView
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
            AddCol(_gridNotifications, "Time", "Time", 95);
            AddCol(_gridNotifications, "Characteristic", "Characteristic", 230);
            AddCol(_gridNotifications, "Length", "Len", 45);
            AddCol(_gridNotifications, "Hex", "Hex", 320);
            AddCol(_gridNotifications, "Ascii", "ASCII", 180);
            _gridNotifications.DataSource = _notifyBinding;
            grp.Controls.Add(_gridNotifications);
            return grp;
        }

        // ----------------------------------------------------------------------
        private void PopulateGattTree()
        {
            _treeGatt.BeginUpdate();
            _treeGatt.Nodes.Clear();
            foreach (GattServiceView svc in _gatt.Services)
            {
                var sNode = new TreeNode(svc.DisplayName) { Tag = svc };
                foreach (GattCharacteristicView ch in svc.Characteristics)
                {
                    var cNode = new TreeNode(ch.DisplayName + "   [" + ch.PropertiesText + "]") { Tag = ch };
                    foreach (GattDescriptorView d in ch.Descriptors)
                        cNode.Nodes.Add(new TreeNode(d.DisplayName) { Tag = d });
                    sNode.Nodes.Add(cNode);
                }
                _treeGatt.Nodes.Add(sNode);
            }
            _treeGatt.ExpandAll();
            if (_treeGatt.Nodes.Count > 0) _treeGatt.SelectedNode = _treeGatt.Nodes[0];
            _treeGatt.EndUpdate();
        }

        private void OnGattNodeSelected(TreeNode node)
        {
            _selectedChar = node != null ? node.Tag as GattCharacteristicView : null;
            if (_selectedChar != null)
            {
                _lblCharInfo.Text = _selectedChar.DisplayName + "      Properties: " + _selectedChar.PropertiesText;
                _btnRead.Enabled = _selectedChar.CanRead;
                _btnWrite.Enabled = _selectedChar.CanWrite;
                _btnWriteNoResp.Enabled = _selectedChar.CanWriteNoResponse;
                bool sub = _selectedChar.CanNotify || _selectedChar.CanIndicate;
                _btnSubscribe.Enabled = sub && !_gatt.IsSubscribed(_selectedChar);
                _btnUnsubscribe.Enabled = sub && _gatt.IsSubscribed(_selectedChar);
            }
            else
            {
                var d = node != null ? node.Tag as GattDescriptorView : null;
                _lblCharInfo.Text = d != null ? "Descriptor: " + d.DisplayName : "Select a characteristic.";
                _btnRead.Enabled = _btnWrite.Enabled = _btnWriteNoResp.Enabled = _btnSubscribe.Enabled = _btnUnsubscribe.Enabled = false;
                if (d != null) DoReadDescriptor(d);
            }
            UpdateActionStates();
        }

        private async void DoRead()
        {
            if (_selectedChar == null) return;
            try
            {
                byte[] data = await _gatt.ReadAsync(_selectedChar);
                if (data != null) SetGattValue(data);
            }
            catch (Exception ex) { AppLog.Error("Read", ex); }
        }

        private async void DoReadDescriptor(Ble.GattDescriptorView d)
        {
            try
            {
                byte[] data = await _gatt.ReadDescriptorAsync(d);
                if (data != null) SetGattValue(data);
            }
            catch (Exception ex) { AppLog.Error("Descriptor read", ex); }
        }

        private async void DoWrite(bool withResponse)
        {
            if (_selectedChar == null) return;
            byte[] data;
            try { data = ParseWriteData(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not parse the input: " + ex.Message, "Write", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { await _gatt.WriteAsync(_selectedChar, data, withResponse); }
            catch (Exception ex) { AppLog.Error("Write", ex); }
        }

        private async void DoSubscribe()
        {
            if (_selectedChar == null) return;
            try
            {
                bool indicate = !_selectedChar.CanNotify && _selectedChar.CanIndicate;
                if (await _gatt.SubscribeAsync(_selectedChar, indicate)) OnGattNodeSelected(_treeGatt.SelectedNode);
            }
            catch (Exception ex) { AppLog.Error("Subscribe", ex); }
        }

        private async void DoUnsubscribe()
        {
            if (_selectedChar == null) return;
            try
            {
                if (await _gatt.UnsubscribeAsync(_selectedChar)) OnGattNodeSelected(_treeGatt.SelectedNode);
            }
            catch (Exception ex) { AppLog.Error("Unsubscribe", ex); }
        }

        private byte[] ParseWriteData()
        {
            string text = _txtWriteData.Text;
            switch (_cboWriteFormat.SelectedIndex)
            {
                case 1: return HexUtil.FromHex(text);
                case 2: return System.Text.Encoding.UTF8.GetBytes(text);
                case 3:
                    string[] parts = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var b = new byte[parts.Length];
                    for (int i = 0; i < parts.Length; i++) b[i] = (byte)(int.Parse(parts[i].Trim()) & 0xFF);
                    return b;
                default: return HexUtil.ParseInput(text);
            }
        }

        private void SetGattValue(byte[] data)
        {
            _hexGattValue.SetData(data);
            _txtGattAscii.Text = HexUtil.ToAscii(data);
        }

        private void OnGattNotification(GattNotification n)
        {
            SetGattValue(n.Value);
            _notifyBinding.Insert(0, new NotificationRow
            {
                Time = n.Timestamp.ToString("HH:mm:ss.fff"),
                Characteristic = AssignedNumbers.CharacteristicName(n.Characteristic.Uuid),
                Length = n.Value.Length,
                Hex = HexUtil.ToHex(n.Value),
                Ascii = HexUtil.ToAscii(n.Value)
            });
            while (_notifyBinding.Count > 1000) _notifyBinding.RemoveAt(_notifyBinding.Count - 1);
        }

        private async void RediscoverServices()
        {
            if (!_gatt.IsConnected) { MessageBox.Show(this, "Connect to a device first.", "Discover", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try { await _gatt.DiscoverAsync(); } catch (Exception ex) { AppLog.Error("Discover", ex); }
        }

        private void DisconnectGatt()
        {
            _gatt.Disconnect();
            _treeGatt.Nodes.Clear();
            _selectedChar = null;
            _lblCharInfo.Text = "Disconnected.";
        }
    }
}
