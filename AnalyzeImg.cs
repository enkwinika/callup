using System;
using System.Drawing;

class Program {
    static void Main(string[] args) {
        string dir = @"C:\Users\enkwi\source\repos\OnCall\MarketingBanners\";
        using (Bitmap b = new Bitmap(dir + "oncall_official_app_map_1776940613626.png")) {
            int minX = 9999, maxX = 0, minY = 9999, maxY = 0;
            for(int x=600; x<800; x++) {
                for(int y=450; y<550; y++) {
                    Color c = b.GetPixel(x, y);
                    // Match the specific green of the OnCall text (approx R:20, G:180, B:100 but let's be loose)
                    if (c.G > 150 && c.R < 100 && c.B < 150) {
                        if(x<minX) minX=x; if(x>maxX) maxX=x;
                        if(y<minY) minY=y; if(y>maxY) maxY=y;
                    }
                }
            }
            Console.WriteLine(string.Format("Phone Logo Bounds: X {0}-{1}, Y {2}-{3}", minX, maxX, minY, maxY));
        }
    }
}
