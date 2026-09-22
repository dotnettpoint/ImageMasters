using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Applies orientation transforms (rotation) to image pixel buffers.</summary>
public interface IImageTransformService
{
    Task<OperationResult<ImagePixelBuffer>> RotateAsync(ImagePixelBuffer source, RotateDirection direction, CancellationToken cancellationToken = default);
}
