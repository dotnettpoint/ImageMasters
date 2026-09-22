using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Applies brightness/contrast/saturation/sharpen adjustments to image pixel buffers.</summary>
public interface IImageAdjustmentService
{
    Task<OperationResult<ImagePixelBuffer>> ApplyAdjustmentsAsync(ImagePixelBuffer source, ImageAdjustmentRequest request, CancellationToken cancellationToken = default);
}
