using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using BleWorkbench.Core;

namespace BleWorkbench.Forms
{
    /// <summary>Branded "About Beacon" dialog.</summary>
    public class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About Beacon";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(480, 340);
            Font = SystemFonts.MessageBoxFont;
            BackColor = Color.White;
            Icon = Branding.LoadIcon();

            // Icon
            var pic = new PictureBox
            {
                Location = new Point(24, 24),
                Size = new Size(96, 96),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Icon big = Branding.LoadIcon(128);
            if (big != null) pic.Image = big.ToBitmap();
            Controls.Add(pic);

            int x = 140;

            var title = new Label
            {
                Text = Branding.AppName,
                Font = new Font("Segoe UI", 26f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 86, 176),
                AutoSize = true,
                Location = new Point(x - 2, 26)
            };
            Controls.Add(title);

            var tagline = new Label
            {
                Text = Branding.Tagline,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 70, 70),
                AutoSize = true,
                Location = new Point(x, 76)
            };
            Controls.Add(tagline);

            var version = new Label
            {
                Text = "Version " + Branding.Version + "   •   .NET Framework 4.8.1   •   Windows 10/11 BLE",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(x, 102)
            };
            Controls.Add(version);

            var desc = new Label
            {
                Text = "A Bluetooth Low Energy sniffer, GATT explorer and device emulator.\r\n" +
                       "Scan and decode advertisements, browse GATT services with read / write /\r\n" +
                       "notify, capture and export packets, and emulate peripherals or broadcasters.",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = false,
                Location = new Point(28, 140),
                Size = new Size(424, 64)
            };
            Controls.Add(desc);

            var sep = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(28, 214), Size = new Size(424, 2) };
            Controls.Add(sep);

            var oss = new Label
            {
                Text = "Open source project by " + Branding.Author,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Location = new Point(28, 230)
            };
            Controls.Add(oss);

            var link = new LinkLabel
            {
                Text = Branding.GitHubUrl,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                LinkColor = Color.FromArgb(20, 86, 176),
                Location = new Point(28, 254)
            };
            link.LinkClicked += (s, e) => OpenUrl(Branding.GitHubUrl);
            Controls.Add(link);

            var copyright = new Label
            {
                Text = "Released as open source. Ported and extended from the original Python/bleak prototype.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(28, 280)
            };
            Controls.Add(copyright);

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 30),
                Location = new Point(ClientSize.Width - 114, ClientSize.Height - 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the link:\r\n" + url + "\r\n\r\n" + ex.Message,
                    "Open link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
