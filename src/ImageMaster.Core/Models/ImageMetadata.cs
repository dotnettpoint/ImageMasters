namespace ImageMaster.Core.Models;

/// <summary>
/// Metadata associated with an image. Loading tolerates this data being
/// partially or fully corrupt: a failed metadata read never blocks the pixel
/// data from loading, it only adds an entry to <see cref="Warnings"/>.
/// </summary>
public sealed class ImageMetadata
{
    public double DpiX { get; init; } = 96.0;
    public double DpiY { get; init; } = 96.0;

    /// <summary>Human-readable EXIF tag name/value pairs, best-effort.</summary>
    public IReadOnlyDictionary<string, string> ExifTags { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Raw ICC color profile bytes, if present and readable. Not applied/converted, only preserved on save when requested.</summary>
    public byte[]? IccProfile { get; init; }

    /// <summary>Non-fatal problems hit while reading metadata (parsed separately from pixel decoding).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public ImageMetadata WithDpi(double dpiX, double dpiY) => new()
    {
        DpiX = dpiX,
        DpiY = dpiY,
        ExifTags = ExifTags,
        IccProfile = IccProfile,
        Warnings = Warnings
    };
}
