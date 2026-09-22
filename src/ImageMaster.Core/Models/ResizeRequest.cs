namespace ImageMaster.Core.Models;

/// <summary>
/// Describes a resize operation, either by target pixel dimensions or by a
/// percentage of the source size.
/// </summary>
public sealed class ResizeRequest
{
    public bool ByPercentage { get; init; }

    /// <summary>Used when <see cref="ByPercentage"/> is false.</summary>
    public int? TargetWidth { get; init; }

    /// <summary>Used when <see cref="ByPercentage"/> is false.</summary>
    public int? TargetHeight { get; init; }

    /// <summary>Used when <see cref="ByPercentage"/> is true. E.g. 50 = half size.</summary>
    public double? PercentageValue { get; init; }

    public bool MaintainAspectRatio { get; init; } = true;

    public bool PreserveMetadata { get; init; } = true;

    /// <summary>
    /// Validates the request shape (not the resulting pixel dimensions,
    /// which depend on the source image and are checked separately).
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (ByPercentage)
        {
            if (PercentageValue is null or <= 0 or > 1000)
                errors.Add("Percentage must be between greater than 0 and 1000.");
        }
        else
        {
            if (TargetWidth is null or <= 0)
                errors.Add("Target width must be a positive number.");
            if (TargetHeight is null or <= 0)
                errors.Add("Target height must be a positive number.");
        }

        return errors;
    }
}
