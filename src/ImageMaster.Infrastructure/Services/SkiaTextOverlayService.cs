using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>Flattens text overlay layers onto an image's pixel buffer.</summary>
public sealed class SkiaTextOverlayService : ITextOverlayService
{
    private readonly IAppLogger _logger;

    public SkiaTextOverlayService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<OperationResult<ImagePixelBuffer>> ApplyTextLayersAsync(ImagePixelBuffer source, IReadOnlyList<TextOverlayLayer> layers, CancellationToken cancellationToken = default)
    {
        foreach (var layer in layers)
        {
            var errors = layer.Validate();
            if (errors.Count > 0)
                return Task.FromResult(OperationResult<ImagePixelBuffer>.Fail($"Invalid text layer \"{layer.Text}\": {string.Join(" ", errors)}"));
        }

        if (layers.Count == 0)
            return Task.FromResult(OperationResult<ImagePixelBuffer>.Ok(source.Clone()));

        return Task.Run(() =>
        {
            try
            {
                using var bitmap = SkiaBufferConverter.ToSkBitmap(source);
                using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType));
                var canvas = surface.Canvas;
                canvas.DrawBitmap(bitmap, 0, 0);

                foreach (var layer in layers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DrawLayer(canvas, layer);
                }

                canvas.Flush();
                using var resultImage = surface.Snapshot();
                using var resultBitmap = SKBitmap.FromImage(resultImage);
                return OperationResult<ImagePixelBuffer>.Ok(SkiaBufferConverter.ToPixelBuffer(resultBitmap));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to apply text overlay layers.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("Text could not be applied to the image.");
            }
        }, cancellationToken);
    }

    private static void DrawLayer(SKCanvas canvas, TextOverlayLayer layer)
    {
        var weight = layer.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = layer.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        using var typeface = SKTypeface.FromFamilyName(layer.FontFamily, weight, SKFontStyleWidth.Normal, slant)
            ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)layer.FontSizePoints);

        var argb = layer.ArgbColor;
        var baseColor = new SKColor(
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF),
            (byte)((argb >> 24) & 0xFF));

        var alpha = (byte)Math.Clamp(baseColor.Alpha * layer.OpacityPercent / 100.0, 0, 255);

        using var paint = new SKPaint
        {
            Color = baseColor.WithAlpha(alpha),
            IsAntialias = true
        };

        // Text is positioned by its top-left corner in our model; SkiaSharp
        // draws from the text baseline, so offset by the font's ascent.
        var baselineY = (float)layer.Y - font.Metrics.Ascent;
        canvas.DrawText(layer.Text, (float)layer.X, baselineY, font, paint);
    }
}
