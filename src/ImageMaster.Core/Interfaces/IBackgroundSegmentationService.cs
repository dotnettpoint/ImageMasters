using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>
/// AI-based background segmentation: predicts a per-pixel foreground mask
/// and returns the image with its background made transparent, ready to be
/// composited by <see cref="IBackgroundService"/> (fill color or replacement
/// image) exactly like the existing chroma-key modes.
/// </summary>
public interface IBackgroundSegmentationService
{
    /// <summary>
    /// True if a usable local model is currently available, so the UI can
    /// decide whether to offer this feature versus showing a clear
    /// "model not installed" message.
    /// </summary>
    bool IsModelAvailable { get; }

    Task<OperationResult<ImagePixelBuffer>> RemoveBackgroundAsync(ImagePixelBuffer source, CancellationToken cancellationToken = default);
}
