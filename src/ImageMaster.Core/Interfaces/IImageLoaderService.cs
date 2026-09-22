using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>
/// Loads image files. Implementations must decode pixel data independently
/// of metadata: a corrupt or unreadable EXIF/ICC block must never prevent
/// the pixels from loading, and metadata errors are reported via
/// <see cref="OperationResult{T}.Warnings"/>, not treated as load failure.
/// </summary>
public interface IImageLoaderService
{
    /// <summary>Loads the full image at <paramref name="filePath"/>.</summary>
    Task<OperationResult<ImageDocument>> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a downsampled preview, used for thumbnails and for very large
    /// images so the UI never has to hold a full-resolution decode just to
    /// show a preview.
    /// </summary>
    Task<OperationResult<ImagePixelBuffer>> LoadThumbnailAsync(string filePath, int maxDimensionPixels, CancellationToken cancellationToken = default);

    /// <summary>File extensions (without the dot) this loader can attempt to decode.</summary>
    IReadOnlySet<string> SupportedExtensions { get; }
}
