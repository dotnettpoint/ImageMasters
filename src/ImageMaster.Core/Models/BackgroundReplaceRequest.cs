namespace ImageMaster.Core.Models;

/// <summary>
/// Describes a background replacement operation. See
/// <see cref="BackgroundReplaceMode"/> for what each mode does; the
/// chroma-key modes are a simple color-distance threshold, not real
/// segmentation, and work best on images with a fairly uniform background.
/// </summary>
public sealed class BackgroundReplaceRequest
{
    public BackgroundReplaceMode Mode { get; init; }

    /// <summary>ARGB fill color, used by the SolidColor* modes.</summary>
    public uint? ArgbFillColor { get; init; }

    /// <summary>Path to a replacement image, used by the *ToImage modes.</summary>
    public string? ReplacementImagePath { get; init; }

    /// <summary>ARGB color to key out, used by the ChromaKey* modes.</summary>
    public uint? ChromaKeyArgbColor { get; init; }

    /// <summary>0-100; how close a pixel's color must be to the key color to be removed.</summary>
    public int ChromaKeyTolerancePercent { get; init; } = 25;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        switch (Mode)
        {
            case BackgroundReplaceMode.SolidColorForTransparent:
                if (ArgbFillColor is null) errors.Add("A fill color is required.");
                break;
            case BackgroundReplaceMode.ImageForTransparent:
                if (string.IsNullOrWhiteSpace(ReplacementImagePath)) errors.Add("A replacement image is required.");
                break;
            case BackgroundReplaceMode.ChromaKeyToSolidColor:
                if (ArgbFillColor is null) errors.Add("A fill color is required.");
                if (ChromaKeyArgbColor is null) errors.Add("A key color is required.");
                break;
            case BackgroundReplaceMode.ChromaKeyToImage:
                if (string.IsNullOrWhiteSpace(ReplacementImagePath)) errors.Add("A replacement image is required.");
                if (ChromaKeyArgbColor is null) errors.Add("A key color is required.");
                break;
        }

        if (ChromaKeyTolerancePercent is < 0 or > 100)
            errors.Add("Tolerance must be between 0 and 100.");

        return errors;
    }
}
