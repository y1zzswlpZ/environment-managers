using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

static class CreateIcon {
    static GraphicsPath Rounded(float x,float y,float w,float h,float radius) {
        GraphicsPath p=new GraphicsPath();float d=radius*2;
        p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);
        p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
    }
    static void Stroke(Graphics g,Color color,float width,PointF[] points) {
        using(Pen pen=new Pen(color,width)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawBeziers(pen,points);}
    }
    static Bitmap Render(int size) {
        Bitmap canvas=new Bitmap(1024,1024,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(canvas)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.CompositingQuality=CompositingQuality.HighQuality;g.ScaleTransform(4,4);
            using(var background=Rounded(8,8,240,240,55))
            using(var gradient=new LinearGradientBrush(new PointF(22,12),new PointF(234,244),Color.FromArgb(28,57,95),Color.FromArgb(9,21,43)))g.FillPath(gradient,background);
            using(var rim=Rounded(8.8f,8.8f,238.4f,238.4f,54))using(Pen pen=new Pen(Color.FromArgb(46,150,191,239),1.5f))g.DrawPath(pen,rim);
            using(SolidBrush glow=new SolidBrush(Color.FromArgb(14,87,198,232)))g.FillEllipse(glow,29,35,197,180);
            // Two sweeping ribbons evoke both Java steam and switching between runtimes.
            Stroke(g,Color.FromArgb(255,158,75),10,new[]{new PointF(123,41),new PointF(150,61),new PointF(86,66),new PointF(113,87)});
            Stroke(g,Color.FromArgb(255,199,124),7,new[]{new PointF(149,52),new PointF(173,72),new PointF(125,79),new PointF(142,96)});
            using(Pen handle=new Pen(Color.FromArgb(179,225,242),12)){handle.LineJoin=LineJoin.Round;g.DrawArc(handle,154,112,43,46,-96,235);}
            using(GraphicsPath cup=new GraphicsPath()) {
                cup.AddLine(64,112,169,112);cup.AddLine(169,112,162,163);cup.AddBezier(162,163,159,184,139,193,116,193);cup.AddBezier(116,193,91,193,72,184,70,162);cup.CloseFigure();
                using(var fill=new LinearGradientBrush(new PointF(78,115),new PointF(157,190),Color.FromArgb(246,253,255),Color.FromArgb(126,206,230)))g.FillPath(fill,cup);
            }
            using(var coffee=new SolidBrush(Color.FromArgb(27,52,77)))g.FillEllipse(coffee,67,106,100,17);
            using(var edge=new Pen(Color.FromArgb(237,252,255),4))g.DrawArc(edge,66,106,102,18,0,180);
            using(var highlight=new Pen(Color.FromArgb(155,255,255,255),5)){highlight.StartCap=LineCap.Round;highlight.EndCap=LineCap.Round;g.DrawLine(highlight,80,135,84,159);}
            Stroke(g,Color.FromArgb(83,183,215),7,new[]{new PointF(54,199),new PointF(80,214),new PointF(155,218),new PointF(181,200)});
            using(SolidBrush badge=new SolidBrush(Color.FromArgb(29,66,98)))g.FillEllipse(badge,176,174,47,47);
            using(Pen arrow=new Pen(Color.FromArgb(127,225,239),4)){arrow.StartCap=LineCap.Round;arrow.EndCap=LineCap.Round;g.DrawArc(arrow,187,185,25,25,30,240);}
            using(SolidBrush arrowHead=new SolidBrush(Color.FromArgb(127,225,239)))g.FillPolygon(arrowHead,new[]{new PointF(205,182),new PointF(214,190),new PointF(202,192)});
        }
        Bitmap result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(canvas,new Rectangle(0,0,size,size),0,0,1024,1024,GraphicsUnit.Pixel);}
        canvas.Dispose();return result;
    }
    static int Main(string[] args) {
        string target=args[0];Directory.CreateDirectory(target);
        int[] sizes={16,20,24,32,40,48,64,96,128,256};List<byte[]> images=new List<byte[]>();
        foreach(int size in sizes)using(Bitmap b=Render(size))using(MemoryStream stream=new MemoryStream()){b.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());if(size==256)b.Save(Path.Combine(target,"JavaManager.png"),ImageFormat.Png);}
        using(var writer=new BinaryWriter(File.Create(Path.Combine(target,"JavaManager.ico")))) {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);uint offset=(uint)(6+sizes.Length*16);
            for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write((uint)images[i].Length);writer.Write(offset);offset+=(uint)images[i].Length;}
            foreach(byte[] image in images)writer.Write(image);
        }
        using(Bitmap b=Render(512))b.Save(Path.Combine(target,"JavaManager-preview.png"),ImageFormat.Png);
        Console.WriteLine("Generated 10-resolution icon and preview.");return 0;
    }
}
