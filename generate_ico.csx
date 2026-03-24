using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

// Generate ICO with LTI logo
void DrawIcon(Bitmap bmp) {
    using var g = Graphics.FromImage(bmp);
    int s = bmp.Width;
    float scale = s / 128f;
    
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    
    // Background
    g.Clear(Color.FromArgb(13, 17, 23)); // #0d1117
    
    // Border
    using var borderPen = new Pen(Color.FromArgb(48, 54, 61), Math.Max(1, 2 * scale)); // #30363d
    g.DrawRectangle(borderPen, 2*scale, 2*scale, s-4*scale, s-4*scale);
    
    // "LTI" text
    using var font = new Font("Consolas", 38 * scale, FontStyle.Bold);
    using var textBrush = new SolidBrush(Color.FromArgb(88, 166, 255)); // #58a6ff
    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    g.DrawString("LTI", font, textBrush, new RectangleF(0, 0, s, s), sf);
    
    // Green dot
    using var dotBrush = new SolidBrush(Color.FromArgb(63, 185, 80)); // #3fb950
    float dotR = Math.Max(2, 8 * scale);
    g.FillEllipse(dotBrush, 100*scale - dotR, 28*scale - dotR, dotR*2, dotR*2);
}

// Create bitmaps
var bmp256 = new Bitmap(256, 256); DrawIcon(bmp256);
var bmp48 = new Bitmap(48, 48); DrawIcon(bmp48);
var bmp32 = new Bitmap(32, 32); DrawIcon(bmp32);
var bmp16 = new Bitmap(16, 16); DrawIcon(bmp16);

// Write ICO file
using var ms = new MemoryStream();
using var bw = new BinaryWriter(ms);

// ICO header
bw.Write((short)0);  // reserved
bw.Write((short)1);  // type: icon
bw.Write((short)4);  // count

int offset = 6 + 4 * 16; // header + 4 entries
var images = new[] { bmp16, bmp32, bmp48, bmp256 };
var pngData = new byte[4][];

for (int i = 0; i < 4; i++) {
    using var pms = new MemoryStream();
    images[i].Save(pms, System.Drawing.Imaging.ImageFormat.Png);
    pngData[i] = pms.ToArray();
}

for (int i = 0; i < 4; i++) {
    int w = images[i].Width >= 256 ? 0 : images[i].Width;
    int h = images[i].Height >= 256 ? 0 : images[i].Height;
    bw.Write((byte)w);
    bw.Write((byte)h);
    bw.Write((byte)0);   // palette
    bw.Write((byte)0);   // reserved
    bw.Write((short)1);  // planes
    bw.Write((short)32); // bpp
    bw.Write(pngData[i].Length);
    bw.Write(offset);
    offset += pngData[i].Length;
}

for (int i = 0; i < 4; i++) bw.Write(pngData[i]);

File.WriteAllBytes("app_icon.ico", ms.ToArray());
Console.WriteLine("ICO created: " + new FileInfo("app_icon.ico").Length + " bytes");
