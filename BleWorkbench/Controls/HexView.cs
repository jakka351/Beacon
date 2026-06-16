using System.Drawing;
using System.Windows.Forms;
using BleWorkbench.Core;

namespace BleWorkbench.Controls
{
    /// <summary>Read-only monospaced offset/hex/ASCII dump pane.</summary>
    public class HexView : TextBox
    {
        public HexView()
        {
            Multiline = true;
            ReadOnly = true;
            WordWrap = false;
            ScrollBars = ScrollBars.Both;
            BackColor = Color.White;
            Font = new Font("Consolas", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
            BorderStyle = BorderStyle.FixedSingle;
            HideSelection = false;
            Text = "(no data)";
        }

        public void SetData(byte[] data)
        {
            Text = (data == null || data.Length == 0) ? "(no data)" : HexUtil.ToHexDump(data);
            SelectionStart = 0;
            SelectionLength = 0;
        }

        public void Clear2()
        {
            Text = "(no data)";
        }
    }
}
