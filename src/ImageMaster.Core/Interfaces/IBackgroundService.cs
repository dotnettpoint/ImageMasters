using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Replaces or fills an image's background. See <see cref="BackgroundReplaceRequest"/> for supported modes.</summary>
public interface IBackgroundService
{
    Task<OperationResult<ImagePixelBuffer>> ReplaceBackgroundAsync(ImagePixelBuffer source, BackgroundReplaceRequest request, CancellationToken cancellationToken = default);
}
