namespace ImageMaster.Core.Models;

/// <summary>
/// A single editable text layer (caption/watermark) positioned over the
/// image in image-pixel coordinates, before it is flattened onto the pixel
/// buffer.
/// </summary>
public sealed class TextOverlayLayer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSizePoints { get; set; } = 24;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }

    /// <summary>0-100.</summary>
    public int OpacityPercent { get; set; } = 100;

    /// <summary>ARGB text color, e.g. 0xFFFFFFFF for opaque white.</summary>
    public uint ArgbColor { get; set; } = 0xFFFFFFFF;

    /// <summary>Top-left position of the text block, in image pixel coordinates.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Text)) errors.Add("Text cannot be empty.");
        if (string.IsNullOrWhiteSpace(FontFamily)) errors.Add("A font family must be selected.");
        if (FontSizePoints is <= 0 or > 1000) errors.Add("Font size must be between 1 and 1000 points.");
        if (OpacityPercent is < 0 or > 100) errors.Add("Opacity must be between 0 and 100.");
        return errors;
    }
}
