using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PeripheralBatteryMonitor.UI
{
    internal sealed class CenteredIconButton : Button
    {
        private bool hovered;
        protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(hovered ? FlatAppearance.MouseOverBackColor : BackColor);
            if(Image != null) e.Graphics.DrawImageUnscaled(Image,(ClientSize.Width-Image.Width)/2,(ClientSize.Height-Image.Height)/2);
            if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,Width-4,Height-4),ForeColor,BackColor);
        }
    }
    public sealed class DeviceCard : Control
    {
        private string deviceName = "", transport = "", status = "", seen = "";
        private int? level;
        private Color accent;
        private bool hover; private string connectedLabel = "Connected", lowLabel = "Low battery";
        private readonly ToolTip disconnectTip = new ToolTip();
        public Button DisconnectButton { get; private set; }
        public bool CanDisconnect { get; private set; }
        private readonly Bitmap disconnectImage;
        public void SetLabels(string connected, string low) { if(connectedLabel == connected && lowLabel == low) return; connectedLabel = connected; lowLabel = low; Invalidate(); }
        public DeviceCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(246, 134); BackColor = Color.FromArgb(16, 21, 31);
            AccessibleRole = AccessibleRole.Grouping;
            disconnectImage = new Bitmap(16,16);
            using(var g = Graphics.FromImage(disconnectImage))
            using(var pen = new Pen(Color.FromArgb(149,163,183),1.7f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = pen.EndCap = LineCap.Round;
                // Two separated plug halves, rather than a power/off symbol.
                g.DrawLine(pen,1,8,4,8); g.DrawRectangle(pen,4,5,3,6);
                g.DrawLine(pen,7,6,8,6); g.DrawLine(pen,7,10,8,10);
                g.DrawRectangle(pen,11,5,3,6); g.DrawLine(pen,14,8,15,8);
                g.DrawLine(pen,8,2,10,4); g.DrawLine(pen,8,14,10,12);
            }
            DisconnectButton = new CenteredIconButton { Size = new Size(24,24), Location = new Point(Width-34,9),
                Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(27,36,50), Image = disconnectImage, TabStop = true,
                ImageAlign = ContentAlignment.MiddleCenter, TextImageRelation = TextImageRelation.Overlay, Padding = new Padding(0),
                AccessibleName = "Disconnect", AccessibleRole = AccessibleRole.PushButton };
            DisconnectButton.FlatAppearance.BorderSize = 0;
            DisconnectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(75,42,50);
            Controls.Add(DisconnectButton);
        }
        public void SetDisconnectStatus(bool enabled, string caption)
        {
            if(CanDisconnect != enabled) { CanDisconnect = enabled; Invalidate(); }
            DisconnectButton.Visible = enabled;
            if(disconnectTip.GetToolTip(DisconnectButton) != caption)
            { DisconnectButton.AccessibleName = caption; disconnectTip.SetToolTip(DisconnectButton,caption); }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing) { DisconnectButton.Image = null; disconnectTip.Dispose(); }
            base.Dispose(disposing);
            if(disposing) disconnectImage.Dispose();
        }
        public void SetData(string name, string connection, int? percent, string state, string reportTime, Color color)
        {
            if (deviceName == name && transport == connection && level == percent && status == state && seen == reportTime && accent == color) return;
            deviceName = name; transport = connection; level = percent; status = state; seen = reportTime; accent = color;
            AccessibleName = name;
            AccessibleDescription = connection + ", " + (percent.HasValue ? percent + "%" : "Battery unavailable") + ", " + state + ", " + reportTime;
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        private static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        private static void Label(Graphics g, string text, float size, Color color, RectangleF bounds, bool bold)
        {
            using (var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text, font, brush, bounds, format);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(Width / 286f, Height / 154f); const int w = 286, h = 154;
            var muted = Color.FromArgb(149, 163, 183);
            using (var shape = Round(new RectangleF(1, 1, w-3, h-3), 14))
            using (var fill = new SolidBrush(Color.FromArgb(27, 36, 50)))
            using (var border = new Pen(hover ? Color.FromArgb(81, 103, 132) : Color.FromArgb(44, 58, 77)))
            { g.FillPath(fill, shape); g.DrawPath(border, shape); }
            using (var tile = Round(new RectangleF(16, 16, 38, 38), 10))
            using (var fill = new SolidBrush(Color.FromArgb(30, accent))) g.FillPath(fill, tile);
            using (var pen = new Pen(accent, 1.8f)) { pen.StartCap = pen.EndCap = LineCap.Round; DeviceIcon(g, pen, deviceName, 23, 22); }
            Label(g, deviceName, 10, Color.White, new RectangleF(65, 17, w-(CanDisconnect ? 106 : 81), 22), true);
            using (var pen = new Pen(muted, 1.4f)) ConnectionIcon(g, pen, transport, 66, 41);
            Label(g, transport, 8, muted, new RectangleF(81, 39, w-97, 19), false);
            Label(g, level.HasValue ? level + "%" : "--", 27, Color.White, new RectangleF(15, 64, 145, 47), true);
            using (var dot = new SolidBrush(accent)) g.FillEllipse(dot, w-92, 82, 5, 5);
            Label(g, accent == Color.OrangeRed ? lowLabel : connectedLabel, 8, muted, new RectangleF(w-82, 76, 72, 22), false);
            using (var track = Round(new RectangleF(18, 117, w-36, 5), 2.5f))
            using (var fill = new SolidBrush(Color.FromArgb(48, 64, 83))) g.FillPath(fill, track);
            if (level.HasValue && level > 0)
            using (var fill = new SolidBrush(accent)) g.FillRectangle(fill, 18, 117, (w-36) * level.Value / 100f, 5);
            Label(g, seen, 7.5f, muted, new RectangleF(17, 129, w-34, 18), false);
        }
        private static void ConnectionIcon(Graphics g, Pen p, string connection, int x, int y)
        {
            var text = (connection ?? "").ToLowerInvariant();
            if (text.Contains("bluetooth"))
            { g.DrawLines(p, new[] { new Point(x+3,y+2),new Point(x+10,y+8),new Point(x+6,y+12),new Point(x+6,y),new Point(x+10,y+4),new Point(x+3,y+10) }); }
            else if (text.Contains("receiver") || text.Contains("ghz"))
            { g.DrawArc(p,x,y,12,12,215,110); g.DrawArc(p,x+3,y+3,6,6,215,110); g.DrawEllipse(p,x+5,y+9,2,2); }
            else { g.DrawLine(p,x+6,y+1,x+6,y+11); g.DrawLine(p,x+2,y+4,x+6,y+7); g.DrawLine(p,x+10,y+3,x+6,y+7); g.DrawEllipse(p,x+4,y+10,4,3); }
        }
        private static void DeviceIcon(Graphics g, Pen p, string name, int x, int y)
        {
            string text = (name ?? "").ToLowerInvariant();
            if (text.Contains("head") || text.Contains("ear") || text.Contains("buds"))
            { g.DrawArc(p,x,y,24,24,180,180); g.DrawRectangle(p,x,y+12,5,11); g.DrawRectangle(p,x+19,y+12,5,11); }
            else if (text.Contains("keyboard"))
            { g.DrawRectangle(p,x-1,y+4,27,18); for(int row=0;row<2;row++) for(int col=0;col<5;col++) g.DrawLine(p,x+3+col*4,y+8+row*4,x+4+col*4,y+8+row*4); g.DrawLine(p,x+6,y+18,x+19,y+18); }
            else if (text.Contains("mouse") || text.Contains("aj139"))
            { using(var shape=Round(new RectangleF(x+4,y,17,27),8))g.DrawPath(p,shape); g.DrawLine(p,x+12,y+2,x+12,y+10); g.DrawLine(p,x+5,y+11,x+20,y+11); }
            else if (text.Contains("controller") || text.Contains("gamepad"))
            { g.DrawArc(p,x,y+3,25,21,150,240); g.DrawLine(p,x+4,y+23,x+9,y+17); g.DrawLine(p,x+21,y+23,x+16,y+17); g.DrawLine(p,x+5,y+11,x+11,y+11); g.DrawLine(p,x+8,y+8,x+8,y+14); g.DrawEllipse(p,x+18,y+9,2,2); g.DrawEllipse(p,x+21,y+13,2,2); }
            else { using(var shape=Round(new RectangleF(x+3,y,19,27),4))g.DrawPath(p,shape); g.DrawLine(p,x+10,y+23,x+15,y+23); }
        }
    }
}
