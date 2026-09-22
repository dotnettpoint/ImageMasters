using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jfif;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Loads images with SkiaSharp, whose decoder tolerates malformed EXIF/ICC
/// segments far better than the WIC codecs Microsoft Photos relies on - this
/// is what lets ImageMaster open files Photos refuses while Paint (which
/// also uses a more permissive path) can. Pixel decoding and metadata
/// reading are performed and error-handled independently, per the app's
/// requirements: a broken metadata block only produces a warning.
/// </summary>
public sealed class SkiaImageLoaderService : IImageLoaderService
{
    private readonly IAppLogger _logger;

    /// <summary>
    /// Images whose pixel count exceeds this are still loaded in full for
    /// editing, but a downsampled preview is used for the on-screen display
    /// where a full-resolution bitmap would be wasteful. Kept as a constant
    /// here; a future version could make this configurable.
    /// </summary>
    private const long LargeImagePixelThreshold = 40_000_000; // ~40 MP

    public IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "bmp", "gif", "tiff", "tif", "webp" };

    public SkiaImageLoaderService(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult<ImageDocument>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return OperationResult<ImageDocument>.Fail("No file path was provided.");

        if (!File.Exists(filePath))
            return OperationResult<ImageDocument>.Fail($"File not found: {Path.GetFileName(filePath)}");

        byte[] fileBytes;
        try
        {
            fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError("Access denied reading file.", ex);
            return OperationResult<ImageDocument>.Fail("This file can't be read - it may be read-only or in use by another program.");
        }
        catch (IOException ex)
        {
            _logger.LogError("I/O error reading file.", ex);
            return OperationResult<ImageDocument>.Fail("This file could not be read due to a disk error.");
        }

        // --- Pixel decode: the part that must succeed for the load to succeed. ---
        ImagePixelBuffer pixelBuffer;
        try
        {
            using var bitmap = SKBitmap.Decode(fileBytes)
                ?? throw new InvalidDataException("SkiaSharp returned no bitmap for this file.");
            pixelBuffer = SkiaBufferConverter.ToPixelBuffer(bitmap);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to decode image pixels for '{Path.GetFileName(filePath)}'.", ex);
            return OperationResult<ImageDocument>.Fail(
                "This doesn't look like a readable image file, or its format isn't supported.");
        }

        if ((long)pixelBuffer.Width * pixelBuffer.Height > LargeImagePixelThreshold)
        {
            _logger.LogWarning($"Loaded a very large image ({pixelBuffer.Width}x{pixelBuffer.Height}) from '{Path.GetFileName(filePath)}'.");
        }

        // --- Metadata read: best-effort, isolated from the decode above. ---
        var (metadata, warnings) = TryReadMetadata(fileBytes, filePath);

        var format = DetectFormat(filePath);
        var document = new ImageDocument(filePath, format, pixelBuffer, metadata);

        return OperationResult<ImageDocument>.Ok(document, warnings);
    }

    public async Task<OperationResult<ImagePixelBuffer>> LoadThumbnailAsync(string filePath, int maxDimensionPixels, CancellationToken cancellationToken = default)
    {
        if (maxDimensionPixels <= 0)
            return OperationResult<ImagePixelBuffer>.Fail("Thumbnail size must be positive.");

        if (!File.Exists(filePath))
            return OperationResult<ImagePixelBuffer>.Fail($"File not found: {Path.GetFileName(filePath)}");

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            using var original = SKBitmap.Decode(fileBytes)
                ?? throw new InvalidDataException("SkiaSharp returned no bitmap for this file.");

            var scale = Math.Min(1.0, (double)maxDimensionPixels / Math.Max(original.Width, original.Height));
            var targetWidth = Math.Max(1, (int)Math.Round(original.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(original.Height * scale));

            using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High)
                ?? throw new InvalidDataException("Thumbnail resize failed.");

            return OperationResult<ImagePixelBuffer>.Ok(SkiaBufferConverter.ToPixelBuffer(resized));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to build thumbnail for '{Path.GetFileName(filePath)}'.", ex);
            return OperationResult<ImagePixelBuffer>.Fail("Couldn't build a thumbnail for this file.");
        }
    }

    private (ImageMetadata Metadata, List<string> Warnings) TryReadMetadata(byte[] fileBytes, string filePath)
    {
        var warnings = new List<string>();
        double dpiX = 96, dpiY = 96;
        var exifTags = new Dictionary<string, string>();

        try
        {
            using var stream = new MemoryStream(fileBytes);
            var directories = ImageMetadataReader.ReadMetadata(stream);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    if (tag.HasName && tag.Description is not null)
                        exifTags[$"{directory.Name}.{tag.Name}"] = tag.Description;
                }

                foreach (var error in directory.Errors)
                    warnings.Add($"Metadata warning ({directory.Name}): {error}");
            }

            var (readDpiX, readDpiY) = ReadResolutionTags(directories);
            if (readDpiX is > 0) dpiX = readDpiX.Value;
            if (readDpiY is > 0) dpiY = readDpiY.Value;
        }
        catch (Exception ex)
        {
            // Metadata failures never fail the load - only pixel decode errors do.
            _logger.LogWarning($"Metadata could not be fully read for '{Path.GetFileName(filePath)}': {ex.Message}");
            warnings.Add("Some metadata (EXIF/ICC) could not be read and was skipped.");
        }

        var metadata = new ImageMetadata
        {
            DpiX = dpiX,
            DpiY = dpiY,
            ExifTags = exifTags,
            IccProfile = null,
            Warnings = warnings
        };

        return (metadata, warnings);
    }

    /// <summary>
    /// Reads DPI from whichever resolution tags are present (EXIF IFD0 takes
    /// priority since it's the most common source; JFIF is the fallback for
    /// plain JPEGs without EXIF). Any single tag failing to parse is ignored
    /// rather than aborting the whole metadata read.
    /// </summary>
    private static (double? DpiX, double? DpiY) ReadResolutionTags(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (exifIfd0 is not null)
        {
            var hasX = exifIfd0.TryGetDouble(ExifDirectoryBase.TagXResolution, out var exifX);
            var hasY = exifIfd0.TryGetDouble(ExifDirectoryBase.TagYResolution, out var exifY);
            if (hasX || hasY)
                return (hasX ? exifX : null, hasY ? exifY : null);
        }

        var jfif = directories.OfType<JfifDirectory>().FirstOrDefault();
        if (jfif is not null)
        {
            var hasX = jfif.TryGetDouble(JfifDirectory.TagResX, out var jfifX);
            var hasY = jfif.TryGetDouble(JfifDirectory.TagResY, out var jfifY);
            if (hasX || hasY)
                return (hasX ? jfifX : null, hasY ? jfifY : null);
        }

        return (null, null);
    }

    private static ImageFormatType DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => ImageFormatType.Jpeg,
            "png" => ImageFormatType.Png,
            "bmp" => ImageFormatType.Bmp,
            "gif" => ImageFormatType.Gif,
            "tif" or "tiff" => ImageFormatType.Tiff,
            "webp" => ImageFormatType.WebP,
            _ => ImageFormatType.Png
        };
    }
}
