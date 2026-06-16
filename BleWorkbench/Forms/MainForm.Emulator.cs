using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using BleWorkbench.Core;
using BleWorkbench.Models;

namespace BleWorkbench.Forms
{
    public partial class MainForm
    {
        private TabPage BuildEmulatorTab()
        {
            var page = new TabPage("Emulator");
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };

            // ---- Left: definition --------------------------------------------
            var left = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var grpAdv = new GroupBox { Text = "Peripheral / Advertisement", Dock = DockStyle.Top, Height = 196 };
            var advTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            advTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            advTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _txtEmuName = new TextBox { Dock = DockStyle.Fill, Text = "BLE-Workbench" };
            _txtEmuServiceUuid = new TextBox { Dock = DockStyle.Fill, Text = "12345678-1234-5678-1234-56789abcdef0" };
            _chkConnectable = new CheckBox { Text = "Connectable", Checked = true, AutoSize = true };
            _chkDiscoverable = new CheckBox { Text = "Discoverable", Checked = true, AutoSize = true };
            _txtMfgCompany = new TextBox { Dock = DockStyle.Fill, Text = "0xFFFF" };
            _txtMfgData = new TextBox { Dock = DockStyle.Fill, Text = "BA AD F0 0D" };
            advTable.Controls.Add(new Label { Text = "Local Name:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            advTable.Controls.Add(_txtEmuName, 1, 0);
            advTable.Controls.Add(new Label { Text = "Service UUID:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            advTable.Controls.Add(_txtEmuServiceUuid, 1, 1);
            var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            flags.Controls.Add(_chkConnectable);
            flags.Controls.Add(_chkDiscoverable);
            advTable.Controls.Add(new Label { Text = "Options:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            advTable.Controls.Add(flags, 1, 2);
            advTable.Controls.Add(new Label { Text = "Mfg Company:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
            advTable.Controls.Add(_txtMfgCompany, 1, 3);
            advTable.Controls.Add(new Label { Text = "Mfg Data (hex):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
            advTable.Controls.Add(_txtMfgData, 1, 4);
            grpAdv.Controls.Add(advTable);

            var grpChars = new GroupBox { Text = "GATT Characteristics", Dock = DockStyle.Fill };
            _gridEmuChars = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            AddCol(_gridEmuChars, "UuidText", "UUID", 240);
            AddCol(_gridEmuChars, "PropertiesText", "Properties", 230);
            AddCol(_gridEmuChars, "InitialHex", "Initial Value", 200);
            AddCol(_gridEmuChars, "UserDescription", "Description", 160);
            _gridEmuChars.DataSource = _emuChars;

            var addPanel = BuildCharAddPanel();
            grpChars.Controls.Add(_gridEmuChars);
            grpChars.Controls.Add(addPanel);

            left.Controls.Add(grpChars);
            left.Controls.Add(grpAdv);
            split.Panel1.Controls.Add(left);

            // ---- Right: run controls -----------------------------------------
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            var runBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
            _btnStartPeripheral = new Button { Text = "Start Peripheral (GATT server)", Width = 200, Height = 30 };
            _btnStartBroadcast = new Button { Text = "Start Broadcast", Width = 120, Height = 30 };
            _btnStopEmu = new Button { Text = "Stop", Width = 80, Height = 30, Enabled = false };
            _btnStartPeripheral.Click += (s, e) => StartPeripheral();
            _btnStartBroadcast.Click += (s, e) => StartBroadcast();
            _btnStopEmu.Click += (s, e) => StopEmulation();
            runBar.Controls.Add(_btnStartPeripheral);
            runBar.Controls.Add(_btnStartBroadcast);
            runBar.Controls.Add(_btnStopEmu);

            _lblEmuStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Emulator stopped.",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var grpNotify = new GroupBox { Text = "Send Notification (peripheral mode)", Dock = DockStyle.Top, Height = 96 };
            var nt = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            nt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            nt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cboNotifyChar = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _txtNotifyValue = new TextBox { Dock = DockStyle.Fill, Text = "01 02 03" };
            _btnNotify = new Button { Text = "Notify Subscribers", Dock = DockStyle.Left, Width = 160, Enabled = false };
            _btnNotify.Click += (s, e) => DoEmuNotify();
            nt.Controls.Add(new Label { Text = "Characteristic:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            nt.Controls.Add(_cboNotifyChar, 1, 0);
            nt.Controls.Add(new Label { Text = "Value:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            nt.Controls.Add(_txtNotifyValue, 1, 1);
            nt.Controls.Add(_btnNotify, 1, 2);
            grpNotify.Controls.Add(nt);

            var help = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Font = new Font("Segoe UI", 9f),
                Text =
                    "Peripheral mode publishes a connectable GATT server (one primary service with the " +
                    "characteristics below) and starts advertising it. Reads return the stored value; writes " +
                    "update it; notifications are pushed to subscribed centrals." + Environment.NewLine + Environment.NewLine +
                    "Broadcast mode advertises a non-connectable payload (local name, service UUID and the " +
                    "manufacturer data above) using the Windows advertisement publisher." + Environment.NewLine + Environment.NewLine +
                    "Note: Windows manages the local adapter's MAC and some advertising fields; it cannot be " +
                    "overridden from a desktop app. Use an external HCI adapter for full control of those fields."
            };

            right.Controls.Add(help);
            right.Controls.Add(grpNotify);
            right.Controls.Add(_lblEmuStatus);
            right.Controls.Add(runBar);
            split.Panel2.Controls.Add(right);

            page.Controls.Add(split);
            page.Enter += (s, e) => { try { split.SplitterDistance = Math.Max(420, split.Width * 3 / 5); } catch { } };

            SeedDefaultCharacteristic();
            return page;
        }

        private Panel BuildCharAddPanel()
        {
            var panel = new Panel { Dock = DockStyle.Bottom, Height = 96 };

            var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, WrapContents = false };
            row1.Controls.Add(new Label { Text = "UUID:", AutoSize = true, Padding = new Padding(2, 6, 0, 0) });
            _txtCharUuid = new TextBox { Width = 280, Text = "12345678-1234-5678-1234-56789abcdef1" };
            row1.Controls.Add(_txtCharUuid);
            row1.Controls.Add(new Label { Text = "Initial (hex/text):", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
            _txtCharInitial = new TextBox { Width = 150, Text = "hello" };
            row1.Controls.Add(_txtCharInitial);
            row1.Controls.Add(new Label { Text = "Desc:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
            _txtCharDesc = new TextBox { Width = 120 };
            row1.Controls.Add(_txtCharDesc);

            var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, WrapContents = false };
            _chkCharRead = new CheckBox { Text = "Read", AutoSize = true, Checked = true };
            _chkCharWrite = new CheckBox { Text = "Write", AutoSize = true, Checked = true };
            _chkCharWriteNr = new CheckBox { Text = "WriteNoResp", AutoSize = true };
            _chkCharNotify = new CheckBox { Text = "Notify", AutoSize = true, Checked = true };
            _chkCharIndicate = new CheckBox { Text = "Indicate", AutoSize = true };
            var btnAdd = new Button { Text = "Add", Width = 70 };
            var btnRemove = new Button { Text = "Remove", Width = 70 };
            btnAdd.Click += (s, e) => AddEmuCharacteristic();
            btnRemove.Click += (s, e) => RemoveEmuCharacteristic();
            row2.Controls.Add(_chkCharRead);
            row2.Controls.Add(_chkCharWrite);
            row2.Controls.Add(_chkCharWriteNr);
            row2.Controls.Add(_chkCharNotify);
            row2.Controls.Add(_chkCharIndicate);
            row2.Controls.Add(btnAdd);
            row2.Controls.Add(btnRemove);

            panel.Controls.Add(row2);
            panel.Controls.Add(row1);
            return panel;
        }

        private void SeedDefaultCharacteristic()
        {
            if (_emuChars.Count > 0) return;
            Guid g;
            AssignedNumbers.TryParseUuid("12345678-1234-5678-1234-56789abcdef1", out g);
            _emuChars.Add(new CharacteristicDefinition
            {
                Uuid = g,
                Read = true,
                Write = true,
                Notify = true,
                InitialValue = System.Text.Encoding.UTF8.GetBytes("hello"),
                UserDescription = "Demo"
            });
        }

        private void AddEmuCharacteristic()
        {
            Guid uuid;
            if (!AssignedNumbers.TryParseUuid(_txtCharUuid.Text, out uuid))
            {
                MessageBox.Show(this, "Enter a valid 16-bit (e.g. 2A37) or 128-bit UUID.", "Add characteristic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            byte[] initial;
            try { initial = HexUtil.ParseInput(_txtCharInitial.Text); }
            catch { initial = new byte[0]; }

            _emuChars.Add(new CharacteristicDefinition
            {
                Uuid = uuid,
                Read = _chkCharRead.Checked,
                Write = _chkCharWrite.Checked,
                WriteNoResponse = _chkCharWriteNr.Checked,
                Notify = _chkCharNotify.Checked,
                Indicate = _chkCharIndicate.Checked,
                InitialValue = initial,
                UserDescription = _txtCharDesc.Text
            });
        }

        private void RemoveEmuCharacteristic()
        {
            if (_gridEmuChars.CurrentRow == null) return;
            var def = _gridEmuChars.CurrentRow.DataBoundItem as CharacteristicDefinition;
            if (def != null) _emuChars.Remove(def);
        }

        private async void StartPeripheral()
        {
            Guid svc;
            if (!AssignedNumbers.TryParseUuid(_txtEmuServiceUuid.Text, out svc))
            {
                MessageBox.Show(this, "Enter a valid service UUID.", "Start peripheral", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_emuChars.Count == 0)
            {
                MessageBox.Show(this, "Add at least one characteristic.", "Start peripheral", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var def = new PeripheralDefinition
            {
                LocalName = _txtEmuName.Text,
                ServiceUuid = svc,
                Connectable = _chkConnectable.Checked,
                Discoverable = _chkDiscoverable.Checked
            };
            foreach (CharacteristicDefinition cd in _emuChars) def.Characteristics.Add(cd);

            try
            {
                if (await _peripheral.StartPeripheralAsync(def))
                    PopulateNotifyChars(def);
            }
            catch (Exception ex)
            {
                AppLog.Error("Start peripheral", ex);
                MessageBox.Show(this, "Could not start peripheral: " + ex.Message, "Start peripheral", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartBroadcast()
        {
            var def = new AdvertisementDefinition { LocalName = _txtEmuName.Text };

            Guid svc;
            if (AssignedNumbers.TryParseUuid(_txtEmuServiceUuid.Text, out svc)) def.ServiceUuids.Add(svc);

            ushort company;
            if (TryParseCompany(_txtMfgCompany.Text, out company))
            {
                def.CompanyId = company;
                try { def.ManufacturerData = HexUtil.ParseInput(_txtMfgData.Text); } catch { def.ManufacturerData = new byte[0]; }
            }

            try { _peripheral.StartBroadcast(def); }
            catch (Exception ex)
            {
                AppLog.Error("Start broadcast", ex);
                MessageBox.Show(this, "Could not start broadcast: " + ex.Message, "Start broadcast", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool TryParseCompany(string text, out ushort company)
        {
            company = 0;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out company);
            return ushort.TryParse(text, out company);
        }

        private void StopEmulation()
        {
            _peripheral.Stop();
        }

        private void PopulateNotifyChars(PeripheralDefinition def)
        {
            _cboNotifyChar.Items.Clear();
            foreach (CharacteristicDefinition cd in def.Characteristics)
                if (cd.Notify || cd.Indicate)
                    _cboNotifyChar.Items.Add(new UuidItem(cd.Uuid));
            if (_cboNotifyChar.Items.Count > 0) _cboNotifyChar.SelectedIndex = 0;
        }

        private async void DoEmuNotify()
        {
            var item = _cboNotifyChar.SelectedItem as UuidItem;
            if (item == null) return;
            byte[] data;
            try { data = HexUtil.ParseInput(_txtNotifyValue.Text); }
            catch (Exception ex) { MessageBox.Show(this, "Bad value: " + ex.Message); return; }
            try { await _peripheral.NotifyAsync(item.Uuid, data); }
            catch (Exception ex) { AppLog.Error("Notify", ex); }
        }

        private void UpdateEmulatorStatus(EmulatorRole role)
        {
            bool running = role != EmulatorRole.Off;
            _btnStartPeripheral.Enabled = !running;
            _btnStartBroadcast.Enabled = !running;
            _btnStopEmu.Enabled = running;
            _btnNotify.Enabled = role == EmulatorRole.Peripheral;
            switch (role)
            {
                case EmulatorRole.Peripheral: _lblEmuStatus.Text = "Peripheral RUNNING — advertising connectable GATT server."; break;
                case EmulatorRole.BroadcasterOnly: _lblEmuStatus.Text = "Broadcasting advertisement payload."; break;
                default: _lblEmuStatus.Text = "Emulator stopped."; break;
            }
        }
    }

    /// <summary>Combo-box item that displays a UUID's friendly name but carries the Guid.</summary>
    public class UuidItem
    {
        public Guid Uuid { get; private set; }
        public UuidItem(Guid uuid) { Uuid = uuid; }
        public override string ToString() { return AssignedNumbers.CharacteristicName(Uuid); }
    }
}
