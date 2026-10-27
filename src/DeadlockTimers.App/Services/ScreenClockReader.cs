using System.Runtime.InteropServices.WindowsRuntime;   // AsBuffer()
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Gdi = System.Drawing;
using GdiImaging = System.Drawing.Imaging;

namespace DeadlockTimers.App.Services;

public sealed class ScreenClockReader
{
    private readonly OcrEngine _engine;

    public ScreenClockReader()
    {
        _engine = OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                  ?? throw new InvalidOperationException("Windows OCR is not available. Install an English language pack with OCR in Windows Settings.");
    }

    /// Captures the region (physical pixels), upscales 3x, runs OCR. Returns raw OCR text.
    public async Task<string> ReadAsync(int x, int y, int width, int height, string? debugPath = null)
    {
        using var shot = new Gdi.Bitmap(width, height, GdiImaging.PixelFormat.Format32bppArgb);
        using (var g = Gdi.Graphics.FromImage(shot))
            g.CopyFromScreen(x, y, 0, 0, new Gdi.Size(width, height));

        int scale = 3;
        using var big = new Gdi.Bitmap(width * scale, height * scale, GdiImaging.PixelFormat.Format32bppArgb);
        using (var g = Gdi.Graphics.FromImage(big))
        {
            g.InterpolationMode = Gdi.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(shot, 0, 0, big.Width, big.Height);
        }

        if (debugPath != null) big.Save(debugPath, GdiImaging.ImageFormat.Png);

        var rect = new Gdi.Rectangle(0, 0, big.Width, big.Height);
        var data = big.LockBits(rect, GdiImaging.ImageLockMode.ReadOnly, GdiImaging.PixelFormat.Format32bppArgb);
        byte[] pixels;
        try
        {
            pixels = new byte[data.Stride * data.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally { big.UnlockBits(data); }

        // 32bppArgb in memory is B,G,R,A per pixel = Bgra8. Stride == width*4 for 32bpp.
        using var sb = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(), BitmapPixelFormat.Bgra8, big.Width, big.Height, BitmapAlphaMode.Premultiplied);

        var result = await _engine.RecognizeAsync(sb);
        return result.Text;
    }
}
