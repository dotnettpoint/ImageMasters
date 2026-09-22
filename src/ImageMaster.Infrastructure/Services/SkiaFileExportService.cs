using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Encodes and writes image buffers to disk. Refuses to silently overwrite
/// the file the document was opened from unless the caller explicitly
/// allows it - the requirement is "never overwrite the original by default".
/// </summary>
public sealed class SkiaFileExportService : IFileExportService
{
    private readonly IAppLogger _logger;

    public SkiaFileExportService(IAppLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> ValidateOutputPath(string outputPath, string? originalFilePath, bool allowOverwriteOriginal)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errors.Add("An output file path is required.");
            return errors;
        }

        if (!IsValidWindowsPath(outputPath))
        {
            errors.Add("The output path contains invalid characters.");
            return errors;
        }

        if (!allowOverwriteOriginal
            && originalFilePath is not null
            && PathsPointToSameFile(outputPath, originalFilePath))
        {
            errors.Add("Saving would overwrite the original file. Choose a different name, or confirm overwrite explicitly.");
        }

        // Note: a pre-existing file at outputPath that is *not* the original
        // is intentionally not flagged here - the UI layer asks the user to
        // confirm overwrite for that case via a normal Save-As dialog prompt.
        // This method only enforces the "never silently overwrite the
        // original" rule.

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            errors.Add("The destination folder does not exist.");

        return errors;
    }

    public async Task<OperationResult<string>> SaveAsAsync(ImagePixelBuffer buffer, ImageMetadata metadata, string? originalFilePath, ExportRequest request, CancellationToken cancellationToken = default)
    {
        var requestErrors = request.Validate();
        var pathErrors = ValidateOutputPath(request.OutputFilePath, originalFilePath, request.AllowOverwriteOriginal);
        var allErrors = requestErrors.Concat(pathErrors).ToList();
        if (allErrors.Count > 0)
            return OperationResult<string>.Fail(string.Join(" ", allErrors));

        try
        {
            CheckAvailableDiskSpace(request.OutputFilePath, buffer);
        }
        catch (IOException ex)
        {
            return OperationResult<string>.Fail(ex.Message);
        }

        try
        {
            using var bitmap = SkiaBufferConverter.ToSkBitmap(buffer);
            var (encodedFormat, quality) = MapFormat(request.TargetFormat, request.JpegQuality);

            using var image = SKImage.FromBitmap(bitmap);
            using var encodedData = image.Encode(encodedFormat, quality)
                ?? throw new NotSupportedException($"The {request.TargetFormat} format is not supported by the installed image codec.");

            await using var fileStream = File.Open(request.OutputFilePath, FileMode.Create, FileAccess.Write);
            encodedData.SaveTo(fileStream);

            return OperationResult<string>.Ok(request.OutputFilePath);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError("Unsupported export format.", ex);
            return OperationResult<string>.Fail(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError("Access denied writing output file.", ex);
            return OperationResult<string>.Fail("The file couldn't be saved - the destination may be read-only.");
        }
        catch (IOException ex)
        {
            _logger.LogError("I/O error writing output file.", ex);
            return OperationResult<string>.Fail("The file couldn't be saved due to a disk error. Check available disk space and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error while saving.", ex);
            return OperationResult<string>.Fail("The file couldn't be saved due to an unexpected error.");
        }
    }

    private static (SKEncodedImageFormat Format, int Quality) MapFormat(ImageFormatType format, int jpegQuality) => format switch
    {
        ImageFormatType.Jpeg => (SKEncodedImageFormat.Jpeg, jpegQuality),
        ImageFormatType.Png => (SKEncodedImageFormat.Png, 100),
        ImageFormatType.WebP => (SKEncodedImageFormat.Webp, jpegQuality),
        ImageFormatType.Bmp => (SKEncodedImageFormat.Bmp, 100),
        ImageFormatType.Gif => (SKEncodedImageFormat.Gif, 100),
        // SkiaSharp has no TIFF encoder. We surface this as a clean,
        // user-facing "not supported" error rather than crashing, per the
        // app's error-handling requirements. A production build could add a
        // dedicated TIFF encoder (e.g. via libtiff bindings) if needed.
        ImageFormatType.Tiff => throw new NotSupportedException("TIFF export isn't supported by the current image codec on this system."),
        _ => throw new NotSupportedException($"Unrecognized export format: {format}")
    };

    private static void CheckAvailableDiskSpace(string outputPath, ImagePixelBuffer buffer)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (string.IsNullOrEmpty(directory)) return;

        var driveRoot = Path.GetPathRoot(directory);
        if (string.IsNullOrEmpty(driveRoot)) return;

        try
        {
            var drive = new DriveInfo(driveRoot);
            // Generous safety margin over the raw pixel buffer size, since
            // encoding can transiently need extra working memory/disk.
            var requiredBytes = (long)buffer.Stride * buffer.Height + 10 * 1024 * 1024;
            if (drive.AvailableFreeSpace < requiredBytes)
                throw new IOException("There isn't enough free disk space to save this file.");
        }
        catch (ArgumentException)
        {
            // Unrecognized drive (e.g. a UNC path); skip the pre-check and let the actual write surface any real error.
        }
    }

    private static bool IsValidWindowsPath(string path)
    {
        var invalidChars = Path.GetInvalidPathChars();
        return path.IndexOfAny(invalidChars) < 0;
    }

    private static bool PathsPointToSameFile(string pathA, string pathB)
    {
        if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(pathA), Path.GetFullPath(pathB), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
