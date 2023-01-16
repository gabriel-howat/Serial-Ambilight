using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using SlimDX;
using System.Diagnostics;

namespace Ambilight.Helpers {
    public class ScreenAnalysisHelper {

        unsafe public static Color GetColorMirror(Bitmap image) {
            /*
			var pixelSize = 0;

			if (image.PixelFormat == PixelFormat.Format32bppRgb)
				pixelSize = 4;

			if (image.PixelFormat == PixelFormat.Format24bppRgb)
				pixelSize = 3;
                */

            //var bounds = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
            BitmapData _bmd = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, image.PixelFormat);


            int _pixelSize = 3;
            byte* _current = (byte*) (void*) _bmd.Scan0;
            int _nWidth = image.Width * _pixelSize;
            int _nHeight = image.Height;
            int count = 0;
            long r = 0;
            long g = 0;
            long b = 0;

            for (int y = 0; y < _nHeight; y += 2) {
                for (int x = 0; x < _nWidth; x += 2) {
                    if (x % _pixelSize == 0 || x == 0) {
                        var pos = x * _pixelSize;
                        b += _current[pos];
                        g += _current[pos + 1];
                        r += _current[pos + 2];
                        //Debug.WriteLine(_current[pos] + " - " + _current[pos + 1] + " - " + _current[pos + 2]);
                        count++;
                    }
                    _current += 2;

                }
            }

            r = r / count;
            g = g / count;
            b = b / count;
            r = r == 1 ? 0 : r;
            b = b == 1 ? 0 : b;
            image.UnlockBits(_bmd);
            //Debug.WriteLine(r + " - " + g + " - " + b);
            return Color.FromArgb(FormAmbilight.gamma[((int) r > 255 ? 255 : r)], FormAmbilight.gamma[((int) g > 255 ? 255 : g)], FormAmbilight.gamma[((int) b > 255 ? 255 : b)]);

        }

        unsafe public static Color GetColorGDI(Bitmap image) {
            /*
			var pixelSize = 0;

			if (image.PixelFormat == PixelFormat.Format32bppRgb)
				pixelSize = 4;

			if (image.PixelFormat == PixelFormat.Format24bppRgb)
				pixelSize = 3;
                */

            //var bounds = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
            var bounds = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
            var data = image.LockBits(bounds, ImageLockMode.ReadOnly, image.PixelFormat);
            var pixelSize = 3;
            long r = 0;
            long g = 0;
            long b = 0;


            for (int y = 0; y < data.Height; ++y) {
                byte* row = (byte*) data.Scan0 + (y * data.Stride);
                for (int x = 0; x < data.Width; ++x) {
                    var pos = x * pixelSize;
                    b += row[pos];
                    g += row[pos + 1];
                    r += row[pos + 2];
                }
            }

            r = r / (data.Width * data.Height);
            g = g / (data.Width * data.Height);
            b = b / (data.Width * data.Height);
            image.UnlockBits(data);

            return Color.FromArgb(FormAmbilight.gamma[r], FormAmbilight.gamma[g], FormAmbilight.gamma[b]);

        }

        static byte[] bu = new byte[2048];
        static int r = 0;
        static int g = 0;
        static int b = 0;
        static int i = 0;
        static int y = 0;
        static int x = 0;
        public static Color avcs(DataStream gs) {
            r = g = b = i = y = 0;
            for (x = 0; x < gs.Length; x += 2048) {
                gs.Read(bu, 0, 2048);
                r += bu[2];
                g += bu[1];
                b += bu[0];
                i++;
            }

            return Color.FromArgb(r / i, g / i, b / i);
        }

    }
}
