using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>Rotates images by 90-degree increments by drawing onto a rotated canvas.</summary>
public sealed class SkiaImageTransformService : IImageTransformService
{
    private readonly IAppLogger _logger;

    public SkiaImageTransformService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<OperationResult<ImagePixelBuffer>> RotateAsync(ImagePixelBuffer source, RotateDirection direction, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var sourceBitmap = SkiaBufferConverter.ToSkBitmap(source);

                var swapsDimensions = direction is RotateDirection.Clockwise90 or RotateDirection.CounterClockwise90;
                var targetWidth = swapsDimensions ? sourceBitmap.Height : sourceBitmap.Width;
                var targetHeight = swapsDimensions ? sourceBitmap.Width : sourceBitmap.Height;

                var degrees = direction switch
                {
                    RotateDirection.Clockwise90 => 90f,
                    RotateDirection.CounterClockwise90 => -90f,
                    RotateDirection.Rotate180 => 180f,
                    _ => throw new ArgumentOutOfRangeException(nameof(direction))
                };

                using var rotated = new SKBitmap(SkiaBufferConverter.BufferImageInfo(targetWidth, targetHeight));
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(targetWidth / 2f, targetHeight / 2f);
                    canvas.RotateDegrees(degrees);
                    canvas.Translate(-sourceBitmap.Width / 2f, -sourceBitmap.Height / 2f);
                    canvas.DrawBitmap(sourceBitmap, 0, 0);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return OperationResult<ImagePixelBuffer>.Ok(SkiaBufferConverter.ToPixelBuffer(rotated));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Rotate operation failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("The image couldn't be rotated. It may be corrupt or too large.");
            }
        }, cancellationToken);
    }
}
