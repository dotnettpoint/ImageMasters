namespace ImageMaster.Core.Models;

/// <summary>
/// Describes a Save As / export operation. Overwriting the original file is
/// only ever allowed when <see cref="AllowOverwriteOriginal"/> is explicitly
/// set - by default the export service refuses and asks for a new file name.
/// </summary>
public sealed class ExportRequest
{
    public required string OutputFilePath { get; init; }
    public ImageFormatType TargetFormat { get; init; }

    /// <summary>1-100. Ignored for lossless formats (Png, Bmp, Tiff).</summary>
    public int JpegQuality { get; init; } = 90;

    public bool PreserveMetadata { get; init; } = true;

    public bool AllowOverwriteOriginal { get; init; }

    /// <summary>
    /// Background color (ARGB) to composite transparent/semi-transparent
    /// pixels onto when the target format doesn't support alpha (e.g. Jpeg,
    /// Bmp) - so the codec never silently picks an unintended fill color.
    /// Defaults to opaque white when not set.
    /// </summary>
    public uint? BackgroundFillArgb { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(OutputFilePath)) errors.Add("An output file path is required.");
        if (JpegQuality is < 1 or > 100) errors.Add("JPEG quality must be between 1 and 100.");
        return errors;
    }
}
