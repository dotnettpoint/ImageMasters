using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Crops image pixel buffers to a chosen rectangle.</summary>
public interface IImageCropService
{
    Task<OperationResult<ImagePixelBuffer>> CropAsync(ImagePixelBuffer source, CropRequest request, CancellationToken cancellationToken = default);
}
