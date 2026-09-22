using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Changes an image's DPI metadata (print resolution) without resampling pixels.</summary>
public interface IDpiService
{
    OperationResult<ImageMetadata> ChangeDpi(ImageMetadata currentMetadata, DpiChangeRequest request);

    /// <summary>
    /// Rough estimated output file size in bytes for the given buffer and
    /// export settings, shown to the user before saving. This is a heuristic,
    /// not an exact figure - encoders make content-dependent choices actual
    /// size can only be known after encoding.
    /// </summary>
    long EstimateFileSizeBytes(ImagePixelBuffer buffer, ExportRequest exportRequest);
}
