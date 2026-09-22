using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Pure pixel-sampling implementation of <see cref="IImageIntegrityService"/> -
/// no imaging-library dependency, since it only reads raw BGRA8888 bytes.
/// Samples small blocks at each corner (by relative position, so it still
/// works across an edit that changed the image's dimensions) and flags any
/// corner whose average color shifted beyond tolerance.
/// </summary>
public sealed class BackgroundIntegrityChecker : IImageIntegrityService
{
    private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    public BackgroundIntegrityResult CheckBackgroundColor(ImagePixelBuffer before, ImagePixelBuffer after, int toleranceRgb = 10)
    {
        var warnings = new List<string>();

        foreach (var corner in Enum.GetValues<Corner>())
        {
            var beforeAvg = SampleCornerAverage(before, corner);
            var afterAvg = SampleCornerAverage(after, corner);

            var deltaR = Math.Abs(beforeAvg.R - afterAvg.R);
            var deltaG = Math.Abs(beforeAvg.G - afterAvg.G);
            var deltaB = Math.Abs(beforeAvg.B - afterAvg.B);

            if (deltaR > toleranceRgb || deltaG > toleranceRgb || deltaB > toleranceRgb)
            {
                warnings.Add(
                    $"Background color at {corner} shifted from RGB({beforeAvg.R:F0},{beforeAvg.G:F0},{beforeAvg.B:F0}) " +
                    $"to RGB({afterAvg.R:F0},{afterAvg.G:F0},{afterAvg.B:F0}), exceeding the tolerance of {toleranceRgb}.");
            }
        }

        return warnings.Count == 0 ? BackgroundIntegrityResult.Clean : new BackgroundIntegrityResult(warnings);
    }

    private static (double R, double G, double B) SampleCornerAverage(ImagePixelBuffer buffer, Corner corner)
    {
        var blockSize = Math.Max(1, Math.Min(4, Math.Min(buffer.Width, buffer.Height)));

        var startX = corner is Corner.TopRight or Corner.BottomRight ? buffer.Width - blockSize : 0;
        var startY = corner is Corner.BottomLeft or Corner.BottomRight ? buffer.Height - blockSize : 0;

        long sumR = 0, sumG = 0, sumB = 0;
        var count = 0;

        for (var y = startY; y < startY + blockSize; y++)
        {
            var rowOffset = y * buffer.Stride;
            for (var x = startX; x < startX + blockSize; x++)
            {
                var offset = rowOffset + x * 4;
                sumB += buffer.Pixels[offset];
                sumG += buffer.Pixels[offset + 1];
                sumR += buffer.Pixels[offset + 2];
                count++;
            }
        }

        return ((double)sumR / count, (double)sumG / count, (double)sumB / count);
    }
}
