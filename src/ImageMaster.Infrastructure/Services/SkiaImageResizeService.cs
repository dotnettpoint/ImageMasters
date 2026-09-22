using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>Resizes images by target dimensions or percentage, with optional aspect-ratio lock.</summary>
public sealed class SkiaImageResizeService : IImageResizeService
{
    private readonly IAppLogger _logger;

    public SkiaImageResizeService(IAppLogger logger)
    {
        _logger = logger;
    }

    public (int Width, int Height) CalculateTargetDimensions(int sourceWidth, int sourceHeight, ResizeRequest request)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            throw new ArgumentException("Source dimensions must be positive.");

        if (request.ByPercentage)
        {
            var percent = request.PercentageValue ?? 100;
            var width = Math.Max(1, (int)Math.Round(sourceWidth * percent / 100.0));
            var height = Math.Max(1, (int)Math.Round(sourceHeight * percent / 100.0));
            return (width, height);
        }

        var targetWidth = request.TargetWidth ?? sourceWidth;
        var targetHeight = request.TargetHeight ?? sourceHeight;

        if (!request.MaintainAspectRatio)
            return (Math.Max(1, targetWidth), Math.Max(1, targetHeight));

        // Aspect lock: fit within the requested box using whichever dimension
        // the user actually changed. We scale from width first; if that would
        // make height bigger than requested, scale from height instead.
        var scaleFromWidth = (double)targetWidth / sourceWidth;
        var heightAtWidthScale = (int)Math.Round(sourceHeight * scaleFromWidth);

        if (heightAtWidthScale <= targetHeight)
            return (Math.Max(1, targetWidth), Math.Max(1, heightAtWidthScale));

        var scaleFromHeight = (double)targetHeight / sourceHeight;
        var widthAtHeightScale = (int)Math.Round(sourceWidth * scaleFromHeight);
        return (Math.Max(1, widthAtHeightScale), Math.Max(1, targetHeight));
    }

    public Task<OperationResult<ImagePixelBuffer>> ResizeAsync(ImagePixelBuffer source, ResizeRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
            return Task.FromResult(OperationResult<ImagePixelBuffer>.Fail(string.Join(" ", validationErrors)));

        return Task.Run(() =>
        {
            try
            {
                var (targetWidth, targetHeight) = CalculateTargetDimensions(source.Width, source.Height, request);

                using var sourceBitmap = SkiaBufferConverter.ToSkBitmap(source);
                // SKFilterQuality.High is used instead of the newer SKSamplingOptions
                // overload for broader compatibility across SkiaSharp versions.
                using var resized = sourceBitmap.Resize(
                    new SKImageInfo(targetWidth, targetHeight),
                    SKFilterQuality.High);

                if (resized is null)
                    return OperationResult<ImagePixelBuffer>.Fail("Resize failed - the target dimensions may be too large for available memory.");

                cancellationToken.ThrowIfCancellationRequested();
                return OperationResult<ImagePixelBuffer>.Ok(SkiaBufferConverter.ToPixelBuffer(resized));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Resize operation failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("The image couldn't be resized. It may be corrupt or too large.");
            }
        }, cancellationToken);
    }
}
