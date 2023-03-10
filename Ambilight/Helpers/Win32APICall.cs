using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace Ambilight.Helpers
{
    public class Win32APICall
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
        public static extern IntPtr DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        public static extern IntPtr DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", EntryPoint = "BitBlt")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest,
            int nYDest, int nWidth, int nHeight, IntPtr hdcSrc,
            int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc,
            int nWidth, int nHeight);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobjBmp);

        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", EntryPoint = "GetDC")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
        public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static Bitmap screenshot;
        static Screen screen;
        static Graphics gfxScreenshot;

        public static Bitmap GetDesktop(int scr, bool fastMethod)
        {
            if (fastMethod)
            {
                int screenX;
                int screenY;
                IntPtr hBmp;
                IntPtr hdcScreen = GetDC(GetDesktopWindow());
                //Debug.WriteLine(GetDesktopWindow());
                IntPtr hdcCompatible = CreateCompatibleDC(hdcScreen);

                screenX = GetSystemMetrics(0);
                screenY = GetSystemMetrics(1);
                hBmp = CreateCompatibleBitmap(hdcScreen, screenX / 2, screenY);

                if (hBmp != IntPtr.Zero)
                {
                    IntPtr hOldBmp = (IntPtr)SelectObject(hdcCompatible, hBmp);
                    BitBlt(hdcCompatible, 0, 0, screenX / 2, screenY, hdcScreen, 0, 0, 13369376);
                    //Debug.WriteLine(screenX + "-" + screenY);
                    SelectObject(hdcCompatible, hOldBmp);
                    DeleteDC(hdcCompatible);
                    ReleaseDC(GetDesktopWindow(), hdcScreen);

                    Bitmap bmp = System.Drawing.Image.FromHbitmap(hBmp);

                    DeleteObject(hBmp);
                    GC.Collect();

                    return bmp;
                }
                return null;
            }
            else
            {

                screen = Screen.AllScreens[scr];

                screenshot = new Bitmap(screen.Bounds.Width,
           screen.Bounds.Height,
           System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                // Create a graphics object from the bitmap

                gfxScreenshot = Graphics.FromImage(screenshot);
                // Take the screenshot from the upper left corner to the right bottom corner
                gfxScreenshot.CopyFromScreen(
                    screen.Bounds.X,
                    screen.Bounds.Y,
                    0,
                    0,
                    screen.Bounds.Size,
                    CopyPixelOperation.SourceCopy);
                gfxScreenshot.Dispose();
                return screenshot;
            }
            //return null;
        }
    }
}
