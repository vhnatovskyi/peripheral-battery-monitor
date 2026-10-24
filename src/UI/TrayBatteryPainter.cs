using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace PeripheralBatteryMonitor.UI
{
    public static class TrayBatteryPainter
    {
        private static GraphicsPath Round(RectangleF r, float radius)
        {
            float d=radius*2; var p=new GraphicsPath();
            p.AddArc(r.Left,r.Top,d,d,180,90); p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.Left,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
        public static Bitmap Render(int[] levels, bool percentage, int lowThreshold)
        {
            var bitmap=new Bitmap(32,32);
            using(var g=Graphics.FromImage(bitmap))
            {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                var values=(levels ?? new int[0]).Take(4).ToArray();
                if(percentage)
                {
                    var known=values.Where(v=>v>=0).ToArray();
                    using(var font=new Font("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel))
                    using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center })
                        g.DrawString(known.Length>0 ? known.Min().ToString() : "?",font,Brushes.White,new RectangleF(0,0,32,32),format);
                    return bitmap;
                }
                if(values.Length==0) values=new[]{-1};
                for(int i=0;i<values.Length;i++)
                {
                    bool single=values.Length==1;
                    var body=single ? new RectangleF(3,9,23,13) : new RectangleF(1+(i%2)*16,4+(i/2)*16,12,8);
                    float stroke=single ? 1.6f : 1.2f;
                    Color color=values[i]<0 ? Color.FromArgb(145,150,158) : values[i]<=lowThreshold ? Color.FromArgb(255,185,75) : Color.FromArgb(245,247,250);
                    using(var pen=new Pen(color,stroke))
                    using(var brush=new SolidBrush(color))
                    using(var outline=Round(body,single ? 2 : 1.3f))
                    {
                        g.DrawPath(pen,outline);
                        g.FillRectangle(brush,body.Right+1,body.Top+body.Height*.3f,single ? 2 : 1,body.Height*.4f);
                        if(values[i]>0)
                        {
                            float inset=single ? 2.5f : 2;
                            float width=(body.Width-inset*2)*Math.Min(values[i],100)/100f;
                            g.FillRectangle(brush,body.Left+inset,body.Top+inset,Math.Max(1,width),body.Height-inset*2);
                        }
                    }
                }
            }
            return bitmap;
        }
    }
}
