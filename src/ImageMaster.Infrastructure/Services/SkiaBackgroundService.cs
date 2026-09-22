using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Replaces or fills an image's background. Transparent-PNG modes are exact
/// (they composite over the real alpha channel). The chroma-key modes are a
/// simple per-pixel color-distance threshold against a chosen key color -
/// this is a basic heuristic, not true segmentation, and works best on
/// images with a fairly uniform, distinctly-colored background (e.g. a
/// green screen or flat studio backdrop). Complex or textured backgrounds
/// will not be cleanly separated.
/// </summary>
public sealed class SkiaBackgroundService : IBackgroundService
{
    private readonly IAppLogger _logger;

    public SkiaBackgroundService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<OperationResult<ImagePixelBuffer>> ReplaceBackgroundAsync(ImagePixelBuffer source, BackgroundReplaceRequest request, CancellationToken cancellationToken = default)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Task.FromResult(OperationResult<ImagePixelBuffer>.Fail(string.Join(" ", errors)));

        return Task.Run(() =>
        {
            try
            {
                using var sourceBitmap = SkiaBufferConverter.ToSkBitmap(source);
                using var working = sourceBitmap.Copy();

                if (request.Mode is BackgroundReplaceMode.ChromaKeyToSolidColor or BackgroundReplaceMode.ChromaKeyToImage)
                    ApplyChromaKeyAlpha(working, request.ChromaKeyArgbColor!.Value, request.ChromaKeyTolerancePercent);

                cancellationToken.ThrowIfCancellationRequested();

                using var backgroundLayer = BuildBackgroundLayer(working.Width, working.Height, request);
                using var composited = Composite(working, backgroundLayer);

                return OperationResult<ImagePixelBuffer>.Ok(SkiaBufferConverter.ToPixelBuffer(composited));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Background replacement failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("The background could not be replaced for this image.");
            }
        }, cancellationToken);
    }

    /// <summary>Zeroes the alpha of pixels within <paramref name="tolerancePercent"/> color distance of the key color.</summary>
    private static void ApplyChromaKeyAlpha(SKBitmap bitmap, uint keyArgb, int tolerancePercent)
    {
        var keyColor = ArgbToSkColor(keyArgb);
        // Max possible per-channel distance is 255*sqrt(3); scale tolerance (0-100) against that.
        var maxDistance = 255.0 * Math.Sqrt(3);
        var threshold = maxDistance * (tolerancePercent / 100.0);

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var distance = ColorDistance(pixel, keyColor);
                if (distance <= threshold)
                    bitmap.SetPixel(x, y, pixel.WithAlpha(0));
            }
        }
    }

    private static double ColorDistance(SKColor a, SKColor b)
    {
        var dr = a.Red - b.Red;
        var dg = a.Green - b.Green;
        var db = a.Blue - b.Blue;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private SKBitmap BuildBackgroundLayer(int width, int height, BackgroundReplaceRequest request)
    {
        var usesImage = request.Mode is BackgroundReplaceMode.ImageForTransparent or BackgroundReplaceMode.ChromaKeyToImage;

        if (usesImage)
        {
            using var replacementBytes = File.OpenRead(request.ReplacementImagePath!);
            using var replacementBitmap = SKBitmap.Decode(replacementBytes)
                ?? throw new InvalidDataException("Replacement image could not be decoded.");

            // Cover-fit: scale to fill the target size, cropping any excess,
            // so the replacement image fills the frame with no letterboxing.
            return CoverFit(replacementBitmap, width, height);
        }

        var fillColor = ArgbToSkColor(request.ArgbFillColor!.Value);
        var solid = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(solid))
        {
            canvas.Clear(fillColor);
        }
        return solid;
    }

    private static SKBitmap CoverFit(SKBitmap source, int targetWidth, int targetHeight)
    {
        var scale = Math.Max((double)targetWidth / source.Width, (double)targetHeight / source.Height);
        var scaledWidth = (int)Math.Ceiling(source.Width * scale);
        var scaledHeight = (int)Math.Ceiling(source.Height * scale);

        using var scaled = source.Resize(new SKImageInfo(scaledWidth, scaledHeight), SKFilterQuality.High)
            ?? throw new InvalidDataException("Replacement image resize failed.");

        var cropX = (scaledWidth - targetWidth) / 2;
        var cropY = (scaledHeight - targetHeight) / 2;
        var cropRect = new SKRectI(cropX, cropY, cropX + targetWidth, cropY + targetHeight);

        var result = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(result);
        canvas.DrawBitmap(scaled, cropRect, new SKRect(0, 0, targetWidth, targetHeight));
        return result;
    }

    /// <summary>Draws the background layer first, then the (possibly now-transparent) foreground on top.</summary>
    private static SKBitmap Composite(SKBitmap foreground, SKBitmap background)
    {
        var result = new SKBitmap(new SKImageInfo(foreground.Width, foreground.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(result);
        canvas.DrawBitmap(background, 0, 0);
        canvas.DrawBitmap(foreground, 0, 0);
        return result;
    }

    private static SKColor ArgbToSkColor(uint argb) => new(
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF),
        (byte)((argb >> 24) & 0xFF));
}
