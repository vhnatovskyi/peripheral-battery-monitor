using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;

namespace PeripheralBatteryMonitor.UI
{
    public static class TrayBatteryPainter
    {
        // Seven pixel rows keep all three digits of 100 inside a 16px tray icon.
        // Font hinting after a fractional Graphics transform clips narrow glyphs.
        private static readonly string[] Digits = {
            "111101101101101101111", "010110010010010010111",
            "111001001111100100111", "111001001111001001111",
            "101101101111001001001", "111100100111001001111",
            "111100100111101101111", "111001001010010010010",
            "111101101111101101111", "111101101111001001111"
        };
        private static readonly string[] WideDigits = {
            "01110100011001110101110011000101110", "00100011000010000100001000010001110",
            "01110100010000100010001000100011111", "11110000010000101110000010000111110",
            "00010001100101010010111110001000010", "11111100001000011110000010000111110",
            "01110100001000011110100011000101110", "11111000010001000100010000100001000",
            "01110100011000101110100011000101110", "01110100011000101111000010000101110"
        };
        private static void PixelCaption(Graphics g, string label, int size, bool combined,bool lightBackground)
        {
            int columns=label.Length>2 ? 3 : 5;
            int scale=Math.Max(1,size/16), width=(label.Length*(columns+1)-1)*scale;
            int x=(size-width)/2, y=combined ? (size-7*scale)/2 : size-7*scale;
            g.ResetTransform();
            g.SmoothingMode=SmoothingMode.None;
            using(var foreground=new SolidBrush(lightBackground && !combined ? Color.FromArgb(24,36,53) : Color.White))
            for(int i=0;i<label.Length;i++)
            for(int row=0;row<7;row++)
            for(int col=0;col<columns;col++)
            {
                bool lit=label[i]=='-' ? row==3 : (columns==3 ? Digits : WideDigits)[label[i]-'0'][row*columns+col]=='1';
                if(!lit)continue;
                int px=x+i*(columns+1)*scale+col*scale, py=y+row*scale;
                g.FillRectangle(foreground,px,py,scale,scale);
            }
        }
        private static GraphicsPath Round(RectangleF r, float radius)
        {
            float d=radius*2; var p=new GraphicsPath();
            p.AddArc(r.Left,r.Top,d,d,180,90); p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.Left,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
        public static Bitmap Render(int[] levels, Color[] deviceColors, bool combined, bool emphasizePercentage)
        { return Render(levels,deviceColors,combined,emphasizePercentage,32); }
        public static Bitmap Render(int[] levels, Color[] deviceColors, bool combined, bool emphasizePercentage,int iconSize)
        { return Render(levels,deviceColors,combined,emphasizePercentage,iconSize,false); }
        public static Bitmap Render(int[] levels, Color[] deviceColors, bool combined, bool emphasizePercentage,int iconSize,bool lightBackground)
        {
            var bitmap=new Bitmap(iconSize,iconSize);
            using(var g=Graphics.FromImage(bitmap))
            {
                g.ScaleTransform(iconSize/32f,iconSize/32f);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                var values=(levels ?? new int[0]).Take(4).ToArray();
                if(values.Length==0) values=new[]{-1};
                if(combined)using(var background=new SolidBrush(Color.FromArgb(16,24,32)))g.FillEllipse(background,1,1,30,30);
                for(int i=0;i<values.Length;i++)
                {
                    Color color=values[i]<0 ? Color.FromArgb(145,150,158) :
                        deviceColors != null && i<deviceColors.Length ? deviceColors[i] : Color.FromArgb(85,222,173);
                    if(combined)
                    {
                        float segment=360f/values.Length, gap=values.Length>1 ? 10 : 3;
                        float start=-90+i*segment+gap/2, sweep=segment-gap;
                        // A stable colored section identifies each device. Exact levels
                        // remain in the tooltip; the center shows the lowest known level.
                        using(var fill=new Pen(color,3.5f)) g.DrawArc(fill,2,2,28,28,start,sweep);
                        continue;
                    }
                    var body=new RectangleF(2,1,25,10);
                    using(var pen=new Pen(color,2f))
                    using(var brush=new SolidBrush(color))
                    using(var outline=Round(body,2))
                    {
                        g.DrawPath(pen,outline);
                        g.FillRectangle(brush,body.Right+1,body.Top+body.Height*.3f,2,body.Height*.4f);
                        if(values[i]>0)
                        {
                            float inset=2;
                            float width=(body.Width-inset*2)*Math.Min(values[i],100)/100f;
                            g.FillRectangle(brush,body.Left+inset,body.Top+inset,Math.Max(1,width),body.Height-inset*2);
                        }
                    }
                }
                var known=values.Where(v=>v>=0).ToArray();
                string label=known.Length>0 ? known.Min().ToString() : "--";
                if(combined)
                {
                    PixelCaption(g,label,iconSize,true,lightBackground);
                    return bitmap;
                }
                // Keep the percentage on its own baseline below the battery.
                PixelCaption(g,label,iconSize,false,lightBackground);
            }
            return bitmap;
        }
    }
}
