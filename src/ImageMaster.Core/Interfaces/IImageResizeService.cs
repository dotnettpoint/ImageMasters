using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Resizes decoded image pixel buffers.</summary>
public interface IImageResizeService
{
    /// <summary>
    /// Pure calculation (no I/O): given a source size and a request, returns
    /// the actual target width/height that will be produced. Used to drive
    /// live previews and to validate a request before committing to it.
    /// </summary>
    (int Width, int Height) CalculateTargetDimensions(int sourceWidth, int sourceHeight, ResizeRequest request);

    Task<OperationResult<ImagePixelBuffer>> ResizeAsync(ImagePixelBuffer source, ResizeRequest request, CancellationToken cancellationToken = default);
}
