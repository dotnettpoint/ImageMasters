namespace ImageMaster.Core.Models;

/// <summary>
/// Result of comparing a sampled "background" region of an image before and
/// after an edit, to catch unintended color shifts from color-space
/// conversion, alpha compositing, or format re-encoding.
/// </summary>
public sealed class BackgroundIntegrityResult
{
    public bool IsWithinTolerance => Warnings.Count == 0;

    public IReadOnlyList<string> Warnings { get; }

    public BackgroundIntegrityResult(IReadOnlyList<string> warnings)
    {
        Warnings = warnings;
    }

    public static readonly BackgroundIntegrityResult Clean = new(Array.Empty<string>());
}
