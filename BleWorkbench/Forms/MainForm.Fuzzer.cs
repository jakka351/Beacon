using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BleWorkbench.Core;

namespace BleWorkbench.Forms
{
    public partial class MainForm
    {
        private readonly Random _fuzzRng = new Random();

        private TabPage BuildFuzzerTab()
        {
            var page = new TabPage("Fuzzer");
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            var grp = new GroupBox { Text = "GATT Write Fuzzer / Spammer (use responsibly, only on devices you are authorised to test)", Dock = DockStyle.Top, Height = 250 };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _lblFuzzTarget = new Label { Dock = DockStyle.Fill, Text = "Target: (select a writable characteristic in GATT Explorer)", AutoEllipsis = true, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };

            _cboFuzzPattern = new ComboBox { Dock = DockStyle.Left, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboFuzzPattern.Items.AddRange(new object[] { "zeros", "ones", "increment", "walking-bit", "random" });
            _cboFuzzPattern.SelectedIndex = 2;

            _numFuzzMin = new NumericUpDown { Minimum = 0, Maximum = 512, Value = 1, Width = 80 };
            _numFuzzMax = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 20, Width = 80 };
            _numFuzzRate = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 50, Width = 80 };
            _numFuzzCount = new NumericUpDown { Minimum = 1, Maximum = 10000000, Value = 1000, Width = 100 };
            _chkFuzzNoResp = new CheckBox { Text = "Write Without Response", Checked = true, AutoSize = true };

            _btnFuzzStart = new Button { Text = "Start Fuzzing", Width = 120, Height = 28 };
            _btnFuzzStop = new Button { Text = "Stop", Width = 80, Height = 28, Enabled = false };
            _btnFuzzStart.Click += (s, e) => StartFuzz();
            _btnFuzzStop.Click += (s, e) => { _fuzzRunning = false; };

            _lblFuzzStatus = new Label { Dock = DockStyle.Fill, Text = "Idle.", TextAlign = ContentAlignment.MiddleLeft };

            int r = 0;
            t.Controls.Add(_lblFuzzTarget, 0, r); t.SetColumnSpan(_lblFuzzTarget, 2); r++;
            t.Controls.Add(new Label { Text = "Pattern:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_cboFuzzPattern, 1, r); r++;
            t.Controls.Add(new Label { Text = "Min length:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_numFuzzMin, 1, r); r++;
            t.Controls.Add(new Label { Text = "Max length:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_numFuzzMax, 1, r); r++;
            t.Controls.Add(new Label { Text = "Rate (pkts/s):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_numFuzzRate, 1, r); r++;
            t.Controls.Add(new Label { Text = "Max packets:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_numFuzzCount, 1, r); r++;
            t.Controls.Add(new Label { Text = "Mode:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_chkFuzzNoResp, 1, r); r++;

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            btnRow.Controls.Add(_btnFuzzStart);
            btnRow.Controls.Add(_btnFuzzStop);
            t.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, r); t.Controls.Add(btnRow, 1, r); r++;
            t.Controls.Add(new Label { Text = "Status:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); t.Controls.Add(_lblFuzzStatus, 1, r); r++;

            grp.Controls.Add(t);
            outer.Controls.Add(grp);
            page.Controls.Add(outer);
            return page;
        }

        private async void StartFuzz()
        {
            if (_fuzzRunning) return;
            if (!_gatt.IsConnected || _selectedChar == null)
            {
                MessageBox.Show(this, "Connect to a device and select a writable characteristic in the GATT Explorer tab first.",
                    "Fuzzer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!_selectedChar.CanWrite && !_selectedChar.CanWriteNoResponse)
            {
                MessageBox.Show(this, "The selected characteristic is not writable.", "Fuzzer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int min = (int)_numFuzzMin.Value;
            int max = (int)_numFuzzMax.Value;
            if (max < min) { MessageBox.Show(this, "Max length must be >= min length.", "Fuzzer"); return; }
            int rate = (int)_numFuzzRate.Value;
            int limit = (int)_numFuzzCount.Value;
            bool noResp = _chkFuzzNoResp.Checked;
            string pattern = _cboFuzzPattern.SelectedItem as string;
            var ch = _selectedChar;

            _fuzzRunning = true;
            _btnFuzzStart.Enabled = false;
            _btnFuzzStop.Enabled = true;
            AppLog.Warn("Fuzzer started: pattern=" + pattern + ", len " + min + "-" + max + ", " + rate + " pkt/s, max " + limit +
                        (noResp ? ", write-without-response" : ", write-with-response"));

            int sent = 0, failed = 0, i = 0;
            int delayMs = Math.Max(1, 1000 / Math.Max(1, rate));

            try
            {
                while (_fuzzRunning && sent < limit)
                {
                    for (int L = min; L <= max && _fuzzRunning && sent < limit; L++)
                    {
                        byte[] payload = MakeFuzzPayload(pattern, L, i);
                        bool ok = await _gatt.WriteQuietAsync(ch, payload, !noResp);
                        sent++;
                        if (!ok) failed++;
                        i++;
                        if (sent % 25 == 0) _lblFuzzStatus.Text = "Sent " + sent + ", failed " + failed + ", last len " + payload.Length;
                        await Task.Delay(delayMs);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Fuzzer", ex);
            }
            finally
            {
                _fuzzRunning = false;
                _btnFuzzStart.Enabled = true;
                _btnFuzzStop.Enabled = false;
                _lblFuzzStatus.Text = "Done. Sent " + sent + ", failed " + failed + ".";
                AppLog.Warn("Fuzzer stopped. Total sent=" + sent + ", failed=" + failed + ".");
            }
        }

        private byte[] MakeFuzzPayload(string pattern, int n, int i)
        {
            if (n < 0) n = 0;
            var b = new byte[n];
            switch (pattern)
            {
                case "zeros":
                    break; // already zero
                case "ones":
                    for (int j = 0; j < n; j++) b[j] = 0xFF;
                    break;
                case "walking-bit":
                    if (n > 0)
                    {
                        int bit = (n * 8) > 0 ? (i % (n * 8)) : 0;
                        b[bit / 8] = (byte)(1 << (bit % 8));
                    }
                    break;
                case "random":
                    _fuzzRng.NextBytes(b);
                    break;
                default: // increment
                    for (int j = 0; j < n; j++) b[j] = (byte)((i + j) & 0xFF);
                    break;
            }
            return b;
        }
    }
}
