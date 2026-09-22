namespace ImageMaster.Core.Models;

/// <summary>
/// A decoded, in-memory image: raw pixel bytes plus enough info to interpret
/// them. This is the type every editing service reads and writes, so Core
/// stays free of any dependency on the imaging library used to produce it
/// (SkiaSharp, in Infrastructure).
/// </summary>
public sealed class ImagePixelBuffer
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>Bytes per row. May be larger than <c>Width * 4</c> due to alignment.</summary>
    public int Stride { get; }

    public PixelFormatKind Format { get; }

    /// <summary>Raw pixel bytes, length == Stride * Height.</summary>
    public byte[] Pixels { get; }

    public ImagePixelBuffer(int width, int height, int stride, PixelFormatKind format, byte[] pixels)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        if (stride < width * 4) throw new ArgumentOutOfRangeException(nameof(stride), "Stride is too small for the given width and pixel format.");
        if (pixels.Length < stride * height) throw new ArgumentException("Pixel buffer is smaller than Stride * Height.", nameof(pixels));

        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        Pixels = pixels;
    }

    /// <summary>Creates a deep copy, used before any destructive edit so undo/redo has a clean snapshot.</summary>
    public ImagePixelBuffer Clone()
    {
        var copy = new byte[Pixels.Length];
        Buffer.BlockCopy(Pixels, 0, copy, 0, Pixels.Length);
        return new ImagePixelBuffer(Width, Height, Stride, Format, copy);
    }
}
