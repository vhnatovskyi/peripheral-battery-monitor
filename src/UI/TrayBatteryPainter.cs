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
        private static GraphicsPath Round(RectangleF r, float radius)
        {
            float d=radius*2; var p=new GraphicsPath();
            p.AddArc(r.Left,r.Top,d,d,180,90); p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.Left,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
        public static Bitmap Render(int[] levels, Color[] deviceColors, bool combined, bool emphasizePercentage)
        { return Render(levels,deviceColors,combined,emphasizePercentage,32); }
        public static Bitmap Render(int[] levels, Color[] deviceColors, bool combined, bool emphasizePercentage,int iconSize)
        {
            var bitmap=new Bitmap(iconSize,iconSize);
            using(var g=Graphics.FromImage(bitmap))
            {
                g.ScaleTransform(iconSize/32f,iconSize/32f);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                var values=(levels ?? new int[0]).Take(4).ToArray();
                if(values.Length==0) values=new[]{-1};
                for(int i=0;i<values.Length;i++)
                {
                    Color color=values[i]<0 ? Color.FromArgb(145,150,158) :
                        deviceColors != null && i<deviceColors.Length ? deviceColors[i] : Color.FromArgb(85,222,173);
                    if(combined)
                    {
                        float segment=360f/values.Length, gap=values.Length>1 ? 10 : 3;
                        float start=-90+i*segment+gap/2, sweep=segment-gap;
                        using(var track=new Pen(Color.FromArgb(80,color),3)) g.DrawArc(track,2,2,28,28,start,sweep);
                        if(values[i]>0) using(var fill=new Pen(color,3)) g.DrawArc(fill,2,2,28,28,start,sweep*Math.Min(values[i],100)/100f);
                        continue;
                    }
                    var body=new RectangleF(3,1,23,9);
                    using(var pen=new Pen(color,1.6f))
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
                    float size=label.Length>2 ? 13 : 17;
                    using(var font=new Font("Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel))
                    using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap })
                        g.DrawString(label,font,Brushes.White,new RectangleF(3,3,26,26),format);
                    return bitmap;
                }
                // Keep the percentage on its own baseline below the battery.
                using(var font=new Font("Segoe UI",label.Length>2 ? 16 : emphasizePercentage ? 19 : 18,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap })
                    g.DrawString(label,font,Brushes.White,new RectangleF(0,11,32,21),format);
            }
            return bitmap;
        }
    }
}
