using ImageMaster.Core.Models;

namespace ImageMaster.Core.Tests.TestDoubles;

/// <summary>Builds small in-memory pixel buffers for tests, so no real image files are needed.</summary>
public static class PixelBufferFactory
{
    /// <summary>Creates a solid-color BGRA8888 buffer of the given size.</summary>
    public static ImagePixelBuffer CreateSolidColor(int width, int height, byte b, byte g, byte r, byte a)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }

        return new ImagePixelBuffer(width, height, stride, PixelFormatKind.Bgra8888, pixels);
    }
}
