using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;

namespace ImageMaster.Infrastructure.Services;

/// <summary>Changes DPI metadata (print resolution) and gives rough file-size estimates.</summary>
public sealed class SkiaDpiService : IDpiService
{
    public OperationResult<ImageMetadata> ChangeDpi(ImageMetadata currentMetadata, DpiChangeRequest request)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return OperationResult<ImageMetadata>.Fail(string.Join(" ", errors));

        return OperationResult<ImageMetadata>.Ok(currentMetadata.WithDpi(request.DpiX, request.DpiY));
    }

    public long EstimateFileSizeBytes(ImagePixelBuffer buffer, ExportRequest exportRequest)
    {
        var pixelCount = (long)buffer.Width * buffer.Height;

        // Rough bytes-per-pixel heuristics by format/quality. These are only
        // meant to give the user a ballpark before saving, not an exact size.
        double bytesPerPixel = exportRequest.TargetFormat switch
        {
            ImageFormatType.Png or ImageFormatType.Tiff => 1.5,
            ImageFormatType.Bmp => 4.0,
            ImageFormatType.Gif => 1.0,
            ImageFormatType.Jpeg => 0.15 + 0.6 * (exportRequest.JpegQuality / 100.0),
            ImageFormatType.WebP => 0.1 + 0.4 * (exportRequest.JpegQuality / 100.0),
            _ => 1.0
        };

        var estimate = (long)(pixelCount * bytesPerPixel);
        return Math.Max(1024, estimate); // never estimate below 1 KB
    }
}
