using System;
using System.Drawing;
using System.Drawing.Imaging;

class Program {
    static void Main(string[] args) {
        string imgPath = @"C:\Users\enkwi\source\repos\OnCall\Content\img\brand\logo-main.png";
        using (Bitmap bmp = new Bitmap(imgPath)) {
            // First, blank out the "allUp" text
            for (int x = 320; x < bmp.Width; x++) {
                for (int y = 0; y < bmp.Height; y++) {
                    Color c = bmp.GetPixel(x, y);
                    // If it is Blue (text "allUp"), make it white. 
                    // To be safe and clean the anti-aliasing of the text, any pixel that is NOT Teal (roof) is made white.
                    if (!(c.G > 100 && c.B > 80 && c.G > c.B && c.R < 100)) {
                        bmp.SetPixel(x, y, Color.White);
                    }
                }
            }

            // Also, there might be some teal extending past X=360 but let's just keep the teal intact.
            // Crop a square region containing the Roof and the C.
            // Roof Y: 319-457, C Y: ~480-700. Total Y: 315 to 725 (height ~410).
            // X: 0 to 410. That's a perfect 410x410 square.
            Rectangle cropRect = new Rectangle(0, 315, 410, 410);
            using (Bitmap iconBmp = bmp.Clone(cropRect, bmp.PixelFormat)) {
                iconBmp.Save(@"C:\Users\enkwi\source\repos\OnCall\Content\img\icon.png", ImageFormat.Png);
            }
        }
        Console.WriteLine("Icon created successfully!");
    }
}
