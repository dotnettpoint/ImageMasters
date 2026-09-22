namespace ImageMaster.Core.Models;

/// <summary>
/// Describes a combined brightness/contrast/saturation/sharpen adjustment,
/// applied as a single operation. Every value is a percentage where 0 means
/// "no change".
/// </summary>
public sealed class ImageAdjustmentRequest
{
    /// <summary>-100 (fully dark) .. 100 (fully bright), 0 = unchanged.</summary>
    public int BrightnessPercent { get; init; }

    /// <summary>-100 (flat gray) .. 100 (double contrast), 0 = unchanged.</summary>
    public int ContrastPercent { get; init; }

    /// <summary>-100 (grayscale) .. 100 (double saturation), 0 = unchanged.</summary>
    public int SaturationPercent { get; init; }

    /// <summary>0 (unchanged) .. 100 (strong unsharp-mask sharpening).</summary>
    public int SharpenAmount { get; init; }

    /// <summary>True if every value is 0 - applying this request would be a no-op.</summary>
    public bool IsNoOp => BrightnessPercent == 0 && ContrastPercent == 0 && SaturationPercent == 0 && SharpenAmount == 0;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (BrightnessPercent is < -100 or > 100) errors.Add("Brightness must be between -100 and 100.");
        if (ContrastPercent is < -100 or > 100) errors.Add("Contrast must be between -100 and 100.");
        if (SaturationPercent is < -100 or > 100) errors.Add("Saturation must be between -100 and 100.");
        if (SharpenAmount is < 0 or > 100) errors.Add("Sharpen amount must be between 0 and 100.");
        return errors;
    }
}
