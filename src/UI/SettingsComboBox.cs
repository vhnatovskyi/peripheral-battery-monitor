using System.Drawing;
using System.Windows.Forms;
namespace PeripheralBatteryMonitor.UI
{
    public sealed class SettingsComboBox : ComboBox
    {
        public SettingsComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 22;
            BackColor = Color.FromArgb(41,56,75); ForeColor = Color.White;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if(e.Index < 0) return;
            using(var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(53,79,107) : BackColor))
                e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                new Rectangle(e.Bounds.X+6,e.Bounds.Y,e.Bounds.Width-8,e.Bounds.Height),ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
