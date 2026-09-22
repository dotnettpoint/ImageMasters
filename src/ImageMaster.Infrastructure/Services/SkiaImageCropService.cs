using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>Crops images to a pixel rectangle by extracting a subset bitmap.</summary>
public sealed class SkiaImageCropService : IImageCropService
{
    private readonly IAppLogger _logger;

    public SkiaImageCropService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<OperationResult<ImagePixelBuffer>> CropAsync(ImagePixelBuffer source, CropRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = request.Validate(source.Width, source.Height);
        if (validationErrors.Count > 0)
            return Task.FromResult(OperationResult<ImagePixelBuffer>.Fail(string.Join(" ", validationErrors)));

        return Task.Run(() =>
        {
            try
            {
                using var sourceBitmap = SkiaBufferConverter.ToSkBitmap(source);
                using var cropped = new SKBitmap(SkiaBufferConverter.BufferImageInfo(request.Width, request.Height));

                var subsetRect = new SKRectI(request.X, request.Y, request.X + request.Width, request.Y + request.Height);
                var extracted = sourceBitmap.ExtractSubset(cropped, subsetRect);
                if (!extracted)
                    return OperationResult<ImagePixelBuffer>.Fail("The crop area is invalid for this image.");

                cancellationToken.ThrowIfCancellationRequested();
                return OperationResult<ImagePixelBuffer>.Ok(SkiaBufferConverter.ToPixelBuffer(cropped));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Crop operation failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("The image couldn't be cropped. It may be corrupt or too large.");
            }
        }, cancellationToken);
    }
}
