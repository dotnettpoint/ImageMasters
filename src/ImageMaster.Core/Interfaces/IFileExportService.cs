using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>Encodes an image buffer to disk in a chosen format.</summary>
public interface IFileExportService
{
    /// <summary>
    /// Checks the requested output path against the original file path and
    /// overwrite flag, without touching the filesystem. Returns validation
    /// errors (empty = OK to proceed).
    /// </summary>
    IReadOnlyList<string> ValidateOutputPath(string outputPath, string? originalFilePath, bool allowOverwriteOriginal);

    /// <summary>Encodes and writes <paramref name="buffer"/> per <paramref name="request"/>. Returns the final written path on success.</summary>
    Task<OperationResult<string>> SaveAsAsync(ImagePixelBuffer buffer, ImageMetadata metadata, string? originalFilePath, ExportRequest request, CancellationToken cancellationToken = default);
}
