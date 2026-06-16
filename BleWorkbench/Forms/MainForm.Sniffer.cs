using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BleWorkbench.Controls;
using BleWorkbench.Core;
using BleWorkbench.Models;

namespace BleWorkbench.Forms
{
    public partial class MainForm
    {
        private TabPage BuildSnifferTab()
        {
            var page = new TabPage("Packet Sniffer");
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };

            // Filter bar
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(2) };
            bar.Controls.Add(new Label { Text = "Source:", AutoSize = true, Padding = new Padding(2, 7, 0, 0) });
            _cboSourceFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cboSourceFilter.Items.AddRange(new object[] { "All", "ADV", "GATT", "PERIPH", "HCI", "SYS" });
            _cboSourceFilter.SelectedIndex = 0;
            _cboSourceFilter.SelectedIndexChanged += (s, e) => RebuildPacketView();
            bar.Controls.Add(_cboSourceFilter);
            bar.Controls.Add(new Label { Text = "Match (address / type / summary):", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });
            _txtPacketFilter = new TextBox { Width = 240 };
            _txtPacketFilter.TextChanged += (s, e) => RebuildPacketView();
            bar.Controls.Add(_txtPacketFilter);
            _chkPause = new CheckBox { Text = "Pause", AutoSize = true, Padding = new Padding(10, 5, 0, 0) };
            _chkPause.CheckedChanged += (s, e) => { if (!_chkPause.Checked) RebuildPacketView(); };
            bar.Controls.Add(_chkPause);
            _chkAutoScroll = new CheckBox { Text = "Auto-scroll", AutoSize = true, Checked = true, Padding = new Padding(10, 5, 0, 0) };
            bar.Controls.Add(_chkAutoScroll);
            _lblPacketCount = new Label { Text = "0 shown", AutoSize = true, Padding = new Padding(12, 7, 0, 0), ForeColor = SystemColors.GrayText };
            bar.Controls.Add(_lblPacketCount);

            // Packet grid (virtual mode for scale)
            _gridPackets = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            AddPlainCol(_gridPackets, "#", 64);
            AddPlainCol(_gridPackets, "Time", 95);
            AddPlainCol(_gridPackets, "Src", 60);
            AddPlainCol(_gridPackets, "Dir", 45);
            AddPlainCol(_gridPackets, "Type", 130);
            AddPlainCol(_gridPackets, "Address", 130);
            AddPlainCol(_gridPackets, "RSSI", 50);
            AddPlainCol(_gridPackets, "Len", 50);
            AddPlainCol(_gridPackets, "Summary", 520);
            _gridPackets.CellValueNeeded += PacketCellValueNeeded;
            _gridPackets.SelectionChanged += (s, e) => ShowSelectedPacket();

            split.Panel1.Controls.Add(_gridPackets);
            split.Panel1.Controls.Add(bar);

            _hexPacket = new HexView { Dock = DockStyle.Fill };
            var hexHeader = new Label { Dock = DockStyle.Top, Height = 22, Text = "Packet Bytes", Font = new Font(Font, FontStyle.Bold), Padding = new Padding(4, 3, 0, 0) };
            split.Panel2.Controls.Add(_hexPacket);
            split.Panel2.Controls.Add(hexHeader);

            page.Controls.Add(split);
            page.Enter += (s, e) => { try { split.SplitterDistance = Math.Max(220, split.Height * 3 / 5); } catch { } };
            return page;
        }

        private static void AddPlainCol(DataGridView grid, string header, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.True
            });
        }

        private void PacketCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _packetView.Count) return;
            BlePacket p = _packetView[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: e.Value = p.Index; break;
                case 1: e.Value = p.TimeText; break;
                case 2: e.Value = p.SourceText; break;
                case 3: e.Value = p.DirectionText; break;
                case 4: e.Value = p.PacketType; break;
                case 5: e.Value = p.Address; break;
                case 6: e.Value = p.RssiText; break;
                case 7: e.Value = p.LengthText; break;
                case 8: e.Value = p.Summary; break;
            }
        }

        private void FlushPackets()
        {
            BlePacket p;
            bool added = false;
            bool paused = _chkPause != null && _chkPause.Checked;
            int drained = 0;

            while (drained < 5000 && _pendingPackets.TryDequeue(out p))
            {
                drained++;
                if (paused) continue;             // discard from view; still retained in the log
                if (MatchesPacketFilter(p)) { _packetView.Add(p); added = true; }
            }

            if (added)
            {
                _gridPackets.RowCount = _packetView.Count;
                _lblPacketCount.Text = _packetView.Count + " shown";
                if (_chkAutoScroll.Checked && _packetView.Count > 0)
                {
                    try { _gridPackets.FirstDisplayedScrollingRowIndex = _packetView.Count - 1; } catch { }
                }
            }
        }

        private bool MatchesPacketFilter(BlePacket p)
        {
            string src = _cboSourceFilter.SelectedItem as string;
            if (!string.IsNullOrEmpty(src) && src != "All" && p.SourceText != src) return false;

            string f = _txtPacketFilter.Text;
            if (!string.IsNullOrEmpty(f))
            {
                f = f.Trim();
                StringComparison c = StringComparison.OrdinalIgnoreCase;
                if (p.Address.IndexOf(f, c) < 0 && p.PacketType.IndexOf(f, c) < 0 &&
                    (p.Summary == null || p.Summary.IndexOf(f, c) < 0))
                    return false;
            }
            return true;
        }

        private void RebuildPacketView()
        {
            _packetView.Clear();
            foreach (BlePacket p in _packets.Snapshot())
                if (MatchesPacketFilter(p)) _packetView.Add(p);
            _gridPackets.RowCount = _packetView.Count;
            _lblPacketCount.Text = _packetView.Count + " shown";
            _gridPackets.Invalidate();
            if (_chkAutoScroll.Checked && _packetView.Count > 0)
            {
                try { _gridPackets.FirstDisplayedScrollingRowIndex = _packetView.Count - 1; } catch { }
            }
        }

        private void ResetPacketView()
        {
            _packetView.Clear();
            _gridPackets.RowCount = 0;
            _gridPackets.Invalidate();
            _hexPacket.SetData(null);
            _lblPacketCount.Text = "0 shown";
        }

        private void ShowSelectedPacket()
        {
            if (_gridPackets.CurrentRow == null) return;
            int i = _gridPackets.CurrentRow.Index;
            if (i >= 0 && i < _packetView.Count) _hexPacket.SetData(_packetView[i].Raw);
        }

        private void ClearPackets()
        {
            _packets.Clear();
            AppLog.Info("Packet log cleared.");
        }

        private void ExportPackets(ExportKind kind)
        {
            List<BlePacket> all = _packets.Snapshot();
            if (all.Count == 0)
            {
                MessageBox.Show(this, "There are no captured packets to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog())
            {
                switch (kind)
                {
                    case ExportKind.Csv: dlg.Filter = "CSV file (*.csv)|*.csv"; dlg.FileName = "ble-capture.csv"; break;
                    case ExportKind.Text: dlg.Filter = "Text file (*.txt)|*.txt"; dlg.FileName = "ble-capture.txt"; break;
                    case ExportKind.BtSnoop: dlg.Filter = "BTSnoop capture (*.btsnoop;*.cfa)|*.btsnoop;*.cfa"; dlg.FileName = "ble-capture.btsnoop"; break;
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    switch (kind)
                    {
                        case ExportKind.Csv:
                            CaptureExport.ToCsv(dlg.FileName, all);
                            AppLog.Success("Exported " + all.Count + " packets to CSV: " + dlg.FileName);
                            break;
                        case ExportKind.Text:
                            CaptureExport.ToText(dlg.FileName, all);
                            AppLog.Success("Exported " + all.Count + " packets to text: " + dlg.FileName);
                            break;
                        case ExportKind.BtSnoop:
                            int n = CaptureExport.ToBtSnoop(dlg.FileName, all);
                            AppLog.Success("Wrote " + n + " HCI/advertising records to BTSnoop: " + dlg.FileName);
                            MessageBox.Show(this, "Wrote " + n + " records (advertising + HCI) to a BTSnoop file.\r\n" +
                                "GATT operations are not represented in BTSnoop and were skipped.\r\nOpen the file in Wireshark to analyse.",
                                "BTSnoop export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("Export failed", ex);
                    MessageBox.Show(this, "Export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
