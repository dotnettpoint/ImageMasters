using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Applies brightness/contrast/saturation via per-pixel color math, and
/// sharpening via a 3x3 unsharp-mask convolution. Pure pixel-buffer math -
/// no imaging-library dependency - but named for consistency with the
/// other Infrastructure imaging services.
/// </summary>
public sealed class SkiaImageAdjustmentService : IImageAdjustmentService
{
    private readonly IAppLogger _logger;

    public SkiaImageAdjustmentService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<OperationResult<ImagePixelBuffer>> ApplyAdjustmentsAsync(ImagePixelBuffer source, ImageAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
            return Task.FromResult(OperationResult<ImagePixelBuffer>.Fail(string.Join(" ", validationErrors)));

        return Task.Run(() =>
        {
            try
            {
                var contrastFactor = 1 + request.ContrastPercent / 100.0;
                var brightnessOffset = request.BrightnessPercent / 100.0 * 255.0;
                var saturationFactor = 1 + request.SaturationPercent / 100.0;

                var pixels = new byte[source.Pixels.Length];
                Buffer.BlockCopy(source.Pixels, 0, pixels, 0, source.Pixels.Length);

                for (var y = 0; y < source.Height; y++)
                {
                    var rowOffset = y * source.Stride;
                    for (var x = 0; x < source.Width; x++)
                    {
                        var offset = rowOffset + x * 4;
                        var b = pixels[offset];
                        var g = pixels[offset + 1];
                        var r = pixels[offset + 2];

                        var (nr, ng, nb) = AdjustPixel(r, g, b, contrastFactor, brightnessOffset, saturationFactor);
                        pixels[offset] = nb;
                        pixels[offset + 1] = ng;
                        pixels[offset + 2] = nr;
                    }

                    if (y % 64 == 0) cancellationToken.ThrowIfCancellationRequested();
                }

                if (request.SharpenAmount > 0)
                    pixels = Sharpen(pixels, source.Width, source.Height, source.Stride, request.SharpenAmount / 50.0);

                cancellationToken.ThrowIfCancellationRequested();
                return OperationResult<ImagePixelBuffer>.Ok(new ImagePixelBuffer(source.Width, source.Height, source.Stride, source.Format, pixels));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Adjustment operation failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("The image couldn't be adjusted. It may be corrupt or too large.");
            }
        }, cancellationToken);
    }

    /// <summary>Applies contrast, then brightness, then saturation (derived from the result's own luminance) to one pixel.</summary>
    private static (byte R, byte G, byte B) AdjustPixel(byte r, byte g, byte b, double contrastFactor, double brightnessOffset, double saturationFactor)
    {
        var rf = Clamp((r - 128) * contrastFactor + 128 + brightnessOffset);
        var gf = Clamp((g - 128) * contrastFactor + 128 + brightnessOffset);
        var bf = Clamp((b - 128) * contrastFactor + 128 + brightnessOffset);

        var luminance = 0.299 * rf + 0.587 * gf + 0.114 * bf;
        rf = Clamp(luminance + (rf - luminance) * saturationFactor);
        gf = Clamp(luminance + (gf - luminance) * saturationFactor);
        bf = Clamp(luminance + (bf - luminance) * saturationFactor);

        return ((byte)rf, (byte)gf, (byte)bf);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 255);

    /// <summary>Classic 3x3 unsharp-mask kernel (center 1+4k, N/S/E/W -k). Border pixels are left unchanged to avoid bounds checks on every pixel.</summary>
    private static byte[] Sharpen(byte[] pixels, int width, int height, int stride, double amount)
    {
        var output = (byte[])pixels.Clone();

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var offset = y * stride + x * 4;
                for (var channel = 0; channel < 3; channel++) // B, G, R - alpha is left untouched
                {
                    var center = pixels[offset + channel];
                    var up = pixels[offset - stride + channel];
                    var down = pixels[offset + stride + channel];
                    var left = pixels[offset - 4 + channel];
                    var right = pixels[offset + 4 + channel];

                    var value = center * (1 + 4 * amount) - amount * (up + down + left + right);
                    output[offset + channel] = (byte)Math.Clamp(value, 0, 255);
                }
            }
        }

        return output;
    }
}
