using System;
using System.Drawing;
using System.Windows.Forms;
using BleWorkbench.Core;

namespace BleWorkbench.Forms
{
    public partial class MainForm
    {
        private TabPage BuildConsoleTab()
        {
            var page = new TabPage("Console");

            var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            var btnClear = new ToolStripButton("Clear") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnClear.Click += (s, e) => _rtbConsole.Clear();
            bar.Items.Add(btnClear);

            _rtbConsole = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                DetectUrls = false,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            page.Controls.Add(_rtbConsole);
            page.Controls.Add(bar);
            return page;
        }

        private void AppendConsole(LogEntry entry)
        {
            if (_rtbConsole == null) return;

            // Keep the buffer bounded.
            if (_rtbConsole.TextLength > 400000)
            {
                _rtbConsole.Select(0, 120000);
                _rtbConsole.SelectedText = string.Empty;
            }

            _rtbConsole.SelectionStart = _rtbConsole.TextLength;
            _rtbConsole.SelectionLength = 0;

            _rtbConsole.SelectionColor = Color.DimGray;
            _rtbConsole.AppendText("[" + entry.TimeText + "] ");

            _rtbConsole.SelectionColor = ColorFor(entry.Level);
            _rtbConsole.AppendText(LevelTag(entry.Level) + entry.Message + Environment.NewLine);

            _rtbConsole.SelectionStart = _rtbConsole.TextLength;
            _rtbConsole.ScrollToCaret();
        }

        private static Color ColorFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Success: return Color.FromArgb(120, 220, 120);
                case LogLevel.Warn: return Color.Gold;
                case LogLevel.Error: return Color.FromArgb(255, 110, 90);
                case LogLevel.Tx: return Color.FromArgb(110, 190, 255);
                case LogLevel.Rx: return Color.FromArgb(230, 210, 120);
                default: return Color.Gainsboro;
            }
        }

        private static string LevelTag(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Success: return "[OK]  ";
                case LogLevel.Warn: return "[WARN] ";
                case LogLevel.Error: return "[ERR] ";
                case LogLevel.Tx: return "[TX]  ";
                case LogLevel.Rx: return "[RX]  ";
                default: return "[..]  ";
            }
        }
    }
}
