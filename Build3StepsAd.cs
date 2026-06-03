using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

class Program {
    static void Main(string[] args) {
        string baseImgPath = @"C:\Users\enkwi\.gemini\antigravity\brain\e2a7bb81-57a6-4cc4-9f9b-5bd7e2b16df3\ad_background_3_steps_1777374053308.png";
        string logoPath = @"C:\Users\enkwi\source\repos\OnCall\Content\img\brand\logo-main.png";
        string outPath = @"C:\Users\enkwi\source\repos\OnCall\MarketingBanners\facebook_ad_3_steps.png";

        using (Bitmap bg = new Bitmap(baseImgPath))
        using (Bitmap logoOrig = new Bitmap(logoPath))
        using (Bitmap finalImg = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(finalImg)) {
            
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Draw Background
            g.DrawImage(bg, 0, 0, 1024, 1024);

            // Add a soft dark gradient overlay over the whole left side to ensure text legibility
            Rectangle rect = new Rectangle(0, 0, 800, 1024);
            using (LinearGradientBrush b = new LinearGradientBrush(rect, Color.FromArgb(230, 15, 23, 42), Color.Transparent, LinearGradientMode.Horizontal)) {
                g.FillRectangle(b, rect);
            }

            // Process Logo (Make white background transparent, turn dark blue text to white)
            Bitmap whiteLogo = new Bitmap(logoOrig.Width, logoOrig.Height, PixelFormat.Format32bppArgb);
            for(int x=0; x<logoOrig.Width; x++) {
                for(int y=0; y<logoOrig.Height; y++) {
                    Color c = logoOrig.GetPixel(x, y);
                    if (c.R > 240 && c.G > 240 && c.B > 240) {
                        whiteLogo.SetPixel(x, y, Color.Transparent);
                    } else {
                        // Change dark blue text to white for contrast against dark background
                        if (c.B > c.R + 40 && c.G < 150) { 
                            whiteLogo.SetPixel(x, y, Color.White);
                        } else {
                            whiteLogo.SetPixel(x, y, c);
                        }
                    }
                }
            }

            // Draw Logo at top left
            g.DrawImage(whiteLogo, 20, 20, 220, 220); // Scale down

            // Define Typograhpy
            Font headlineFont = new Font("Segoe UI", 36, FontStyle.Bold);
            Font bodyFont = new Font("Segoe UI", 18, FontStyle.Regular);
            Font stepsFont = new Font("Segoe UI", 22, FontStyle.Bold);
            Font footerFont = new Font("Segoe UI", 16, FontStyle.Italic);
            
            int textX = 60;
            int currentY = 220;

            // Headline
            string headline = "Get your home sorted\nin 3 simple steps.";
            g.DrawString(headline, headlineFont, Brushes.White, new PointF(textX, currentY));
            currentY += 120;

            // Body
            string bodyText = "Finding a trustworthy handyman shouldn't\nfeel like a gamble. With CallUp, it's as easy as:";
            g.DrawString(bodyText, bodyFont, new SolidBrush(Color.FromArgb(226, 232, 240)), new PointF(textX, currentY));
            currentY += 80;

            // 3 Steps
            SolidBrush primaryBrush = new SolidBrush(Color.FromArgb(23, 162, 184)); // Teal
            
            g.DrawString("1. Post your job in seconds.", stepsFont, primaryBrush, new PointF(textX, currentY));
            currentY += 50;
            
            g.DrawString("2. Compare live bids from verified local pros.", stepsFont, primaryBrush, new PointF(textX, currentY));
            currentY += 50;
            
            g.DrawString("3. Choose your expert and relax while it gets done!", stepsFont, primaryBrush, new PointF(textX, currentY));
            currentY += 80;

            // Footer / Secondary text
            string noHiddenFees = "No hidden fees, no unreliable contractors.\nJust the best pros, closest to you.";
            g.DrawString(noHiddenFees, footerFont, Brushes.LightGray, new PointF(textX, currentY));
            currentY += 80;

            // CTA Button
            int btnWidth = 550;
            int btnHeight = 60;
            using (GraphicsPath btnPath = new GraphicsPath()) {
                int r = 30; // High border radius for pill shape
                btnPath.AddArc(textX, currentY, r, r, 180, 90);
                btnPath.AddArc(textX + btnWidth - r, currentY, r, r, 270, 90);
                btnPath.AddArc(textX + btnWidth - r, currentY + btnHeight - r, r, r, 0, 90);
                btnPath.AddArc(textX, currentY + btnHeight - r, r, r, 90, 90);
                btnPath.CloseFigure();
                g.FillPath(new SolidBrush(Color.FromArgb(20, 100, 180)), btnPath);
            }
            
            Font btnFont = new Font("Segoe UI", 20, FontStyle.Bold);
            string btnText = "Start your first request for free at www.callup.co.za";
            
            // Measure text to center it (rough approximation)
            g.DrawString("Start your free request at www.callup.co.za", btnFont, Brushes.White, new PointF(textX + 25, currentY + 12));

            finalImg.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine("3 Steps Ad built successfully!");
    }
}
