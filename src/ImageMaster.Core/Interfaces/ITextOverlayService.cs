using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Draws (flattens) text layers onto an image pixel buffer.</summary>
public interface ITextOverlayService
{
    /// <summary>
    /// Returns a new buffer with all <paramref name="layers"/> drawn onto a
    /// copy of <paramref name="source"/>, in list order (later layers drawn
    /// on top). The source buffer is not modified.
    /// </summary>
    Task<OperationResult<ImagePixelBuffer>> ApplyTextLayersAsync(ImagePixelBuffer source, IReadOnlyList<TextOverlayLayer> layers, CancellationToken cancellationToken = default);
}
