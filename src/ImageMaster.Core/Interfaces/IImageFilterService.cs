using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Applies a one-click stylistic filter to an image pixel buffer.</summary>
public interface IImageFilterService
{
    Task<OperationResult<ImagePixelBuffer>> ApplyFilterAsync(ImagePixelBuffer source, ImageFilterType filterType, CancellationToken cancellationToken = default);
}
