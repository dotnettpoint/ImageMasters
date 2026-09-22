namespace ImageMaster.Core.Models;

/// <summary>Describes a crop: a pixel rectangle to extract from the source image.</summary>
public sealed class CropRequest
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Validates the rectangle against the source image's actual dimensions.</summary>
    public IReadOnlyList<string> Validate(int sourceWidth, int sourceHeight)
    {
        var errors = new List<string>();

        if (Width <= 0) errors.Add("Crop width must be a positive number.");
        if (Height <= 0) errors.Add("Crop height must be a positive number.");
        if (X < 0 || Y < 0) errors.Add("Crop position cannot be negative.");
        if (Width > 0 && X + Width > sourceWidth) errors.Add("The crop area extends past the right edge of the image.");
        if (Height > 0 && Y + Height > sourceHeight) errors.Add("The crop area extends past the bottom edge of the image.");

        return errors;
    }
}
