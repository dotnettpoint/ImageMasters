using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Converts between our library-agnostic <see cref="ImagePixelBuffer"/> and
/// SkiaSharp's <see cref="SKBitmap"/>. Kept in one place so every service
/// normalizes to the same pixel layout (BGRA8888, straight-alpha, no row
/// padding) instead of each service reinventing the conversion.
/// </summary>
internal static class SkiaBufferConverter
{
    public static SKImageInfo BufferImageInfo(int width, int height) =>
        new(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

    public static SKBitmap ToSkBitmap(ImagePixelBuffer buffer)
    {
        var info = BufferImageInfo(buffer.Width, buffer.Height);
        var bitmap = new SKBitmap(info);

        unsafe
        {
            fixed (byte* src = buffer.Pixels)
            {
                var dst = bitmap.GetPixels();
                for (var row = 0; row < buffer.Height; row++)
                {
                    var srcRow = src + row * buffer.Stride;
                    var dstRow = (byte*)dst + row * bitmap.RowBytes;
                    Buffer.MemoryCopy(srcRow, dstRow, bitmap.RowBytes, buffer.Width * 4);
                }
            }
        }

        return bitmap;
    }

    public static ImagePixelBuffer ToPixelBuffer(SKBitmap bitmap)
    {
        // Normalize to Bgra8888/Unpremul so every buffer in the app has an
        // identical, predictable layout regardless of how it was decoded.
        using var normalized = bitmap.ColorType == SKColorType.Bgra8888 && bitmap.AlphaType == SKAlphaType.Unpremul
            ? null
            : bitmap.Copy(SKColorType.Bgra8888);

        var source = normalized ?? bitmap;
        var stride = source.RowBytes;
        var pixels = new byte[stride * source.Height];

        System.Runtime.InteropServices.Marshal.Copy(source.GetPixels(), pixels, 0, pixels.Length);

        return new ImagePixelBuffer(source.Width, source.Height, stride, PixelFormatKind.Bgra8888, pixels);
    }
}
