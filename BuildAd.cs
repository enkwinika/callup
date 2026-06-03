using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

class Program {
    static void Main(string[] args) {
        string baseImgPath = @"C:\Users\enkwi\.gemini\antigravity\brain\e2a7bb81-57a6-4cc4-9f9b-5bd7e2b16df3\base_marketing_photo_1777373228757.png";
        string logoPath = @"C:\Users\enkwi\source\repos\OnCall\Content\img\brand\logo-main.png";
        string outPath = @"C:\Users\enkwi\source\repos\OnCall\MarketingBanners\facebook_ad_easy_understand.png";

        using (Bitmap bg = new Bitmap(baseImgPath))
        using (Bitmap logoOrig = new Bitmap(logoPath))
        using (Bitmap finalImg = new Bitmap(bg.Width, bg.Height, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(finalImg)) {
            
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. Draw Background
            g.DrawImage(bg, 0, 0, bg.Width, bg.Height);

            // 2. Add Dark Gradient Overlay at the bottom for text readability
            Rectangle rect = new Rectangle(0, bg.Height / 2, bg.Width, bg.Height / 2);
            using (LinearGradientBrush b = new LinearGradientBrush(rect, Color.Transparent, Color.FromArgb(220, 0, 0, 0), LinearGradientMode.Vertical)) {
                g.FillRectangle(b, rect);
            }

            // 3. Process Logo (Make white background transparent)
            Bitmap logo = new Bitmap(logoOrig.Width, logoOrig.Height, PixelFormat.Format32bppArgb);
            for(int x=0; x<logoOrig.Width; x++) {
                for(int y=0; y<logoOrig.Height; y++) {
                    Color c = logoOrig.GetPixel(x, y);
                    if (c.R > 240 && c.G > 240 && c.B > 240) {
                        logo.SetPixel(x, y, Color.Transparent);
                    } else {
                        logo.SetPixel(x, y, c);
                    }
                }
            }

            // 4. Draw Logo on a White Pill for maximum contrast
            int logoWidth = 250;
            int logoHeight = 250;
            int pillX = 40;
            int pillY = 40;
            int pillW = 220;
            int pillH = 100;
            using (GraphicsPath path = new GraphicsPath()) {
                int r = 20;
                path.AddArc(pillX, pillY, r, r, 180, 90);
                path.AddArc(pillX + pillW - r, pillY, r, r, 270, 90);
                path.AddArc(pillX + pillW - r, pillY + pillH - r, r, r, 0, 90);
                path.AddArc(pillX, pillY + pillH - r, r, r, 90, 90);
                path.CloseFigure();
                g.FillPath(Brushes.White, path);
            }
            // Crop out just the useful part of the logo if possible, but resizing is fine
            // We'll draw the logo roughly inside the pill. The logo itself has some padding.
            g.DrawImage(logo, pillX - 10, pillY - 70, logoWidth, logoHeight);

            // 5. Draw Typography
            Font titleFont = new Font("Segoe UI", 48, FontStyle.Bold);
            Font subFont = new Font("Segoe UI", 28, FontStyle.Bold);
            Font bodyFont = new Font("Segoe UI", 24, FontStyle.Regular);

            int textY = bg.Height - 380;
            g.DrawString("HOME REPAIRS MADE EASY", titleFont, Brushes.White, new PointF(40, textY));
            
            g.DrawString("1. Post your job", subFont, new SolidBrush(Color.FromArgb(23, 162, 184)), new PointF(40, textY + 80));
            g.DrawString("2. Compare local pros", subFont, new SolidBrush(Color.FromArgb(23, 162, 184)), new PointF(40, textY + 130));
            g.DrawString("3. Relax while it's done", subFont, new SolidBrush(Color.FromArgb(23, 162, 184)), new PointF(40, textY + 180));

            g.DrawString("✓ Upfront pricing   ✓ Verified experts   ✓ Secure payments", bodyFont, Brushes.White, new PointF(40, textY + 250));

            // Call to Action Button
            int btnX = 40;
            int btnY = textY + 310;
            using (GraphicsPath btnPath = new GraphicsPath()) {
                int r = 15;
                btnPath.AddArc(btnX, btnY, r, r, 180, 90);
                btnPath.AddArc(btnX + 350 - r, btnY, r, r, 270, 90);
                btnPath.AddArc(btnX + 350 - r, btnY + 70 - r, r, r, 0, 90);
                btnPath.AddArc(btnX, btnY + 70 - r, r, r, 90, 90);
                btnPath.CloseFigure();
                g.FillPath(new SolidBrush(Color.FromArgb(20, 100, 180)), btnPath);
            }
            Font btnFont = new Font("Segoe UI", 24, FontStyle.Bold);
            g.DrawString("www.callup.co.za", btnFont, Brushes.White, new PointF(btnX + 25, btnY + 15));

            finalImg.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine("Image built successfully!");
    }
}
