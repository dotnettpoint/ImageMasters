namespace ImageMaster.Core.Models;

/// <summary>
/// Changes the DPI stamped in an image's metadata without touching pixel
/// dimensions - i.e. how the same pixels print, not how they look on screen.
/// </summary>
public sealed class DpiChangeRequest
{
    public double DpiX { get; init; }
    public double DpiY { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        // 1-2400 covers everything from very coarse screen output to high-end print scans.
        if (DpiX is <= 0 or > 2400) errors.Add("Horizontal DPI must be between 1 and 2400.");
        if (DpiY is <= 0 or > 2400) errors.Add("Vertical DPI must be between 1 and 2400.");
        return errors;
    }
}
