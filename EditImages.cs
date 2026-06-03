using System;
using System.Drawing;
using System.Drawing.Imaging;

class Program {
    static void Main(string[] args) {
        string dir = @"C:\Users\enkwi\source\repos\OnCall\MarketingBanners\";
        string logoFile = @"C:\Users\enkwi\source\repos\OnCall\Content\img\brand\logo-main.png";
        
        using (Bitmap origLogo = new Bitmap(logoFile)) {
            // Create White-Text Transparent Logo
            Bitmap whiteLogo = new Bitmap(origLogo.Width, origLogo.Height, PixelFormat.Format32bppArgb);
            // Create Dark-Text Transparent Logo
            Bitmap darkLogo = new Bitmap(origLogo.Width, origLogo.Height, PixelFormat.Format32bppArgb);
            
            for(int x=0; x<origLogo.Width; x++) {
                for(int y=0; y<origLogo.Height; y++) {
                    Color c = origLogo.GetPixel(x, y);
                    if (c.R > 240 && c.G > 240 && c.B > 240) {
                        whiteLogo.SetPixel(x, y, Color.Transparent);
                        darkLogo.SetPixel(x, y, Color.Transparent);
                    } else {
                        // Keep text/shape for dark logo
                        darkLogo.SetPixel(x, y, c);
                        
                        // For white logo, change dark blue text to white
                        if (c.B > c.R + 40 && c.G < 150) { // Dark Blue
                            whiteLogo.SetPixel(x, y, Color.White);
                        } else {
                            whiteLogo.SetPixel(x, y, c); // Keep teal roof
                        }
                    }
                }
            }

            // Edit Image 1
            using (Bitmap b1 = new Bitmap(dir + "oncall_square_banner_option1_1776943734924.png")) {
                using (Graphics g = Graphics.FromImage(b1)) {
                    // Erase old logo by streaking wall from X=380
                    for(int x = 20; x < 350; x++) {
                        for(int y = 5; y < 150; y++) {
                            b1.SetPixel(x, y, b1.GetPixel(380, y));
                        }
                    }
                    g.DrawImage(whiteLogo, 30, 25, 230, 230); // 1024 / ~4 = 250 -> scaled logo
                }
                b1.Save(dir + "oncall_square_banner_option1_1776943734924_fixed.png", ImageFormat.Png);
            }

            // Edit Image 2
            using (Bitmap b2 = new Bitmap(dir + "oncall_official_app_map_1776940613626.png")) {
                using (Graphics g = Graphics.FromImage(b2)) {
                    // Erase old logo in phone header
                    g.FillRectangle(Brushes.White, 660, 490, 100, 35);
                    // Draw new logo
                    g.DrawImage(darkLogo, 665, 490, 85, 85);
                }
                b2.Save(dir + "oncall_official_app_map_1776940613626_fixed.png", ImageFormat.Png);
            }
        }
        Console.WriteLine("Images updated!");
    }
}
