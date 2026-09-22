using ImageMaster.Core.Models;

namespace ImageMaster.Core.Interfaces;

/// <summary>
/// Verifies that an edit didn't unintentionally shift the image's
/// background/canvas color (e.g. via color-space conversion, alpha
/// compositing, or format re-encoding), by sampling corner regions before
/// and after the operation.
/// </summary>
public interface IImageIntegrityService
{
    /// <summary>
    /// Samples a small block at each corner of <paramref name="before"/> and
    /// the correspondingly-positioned corner of <paramref name="after"/>
    /// (positions are relative, so this still works across a resize/crop that
    /// changed the image's dimensions), and reports any corner whose average
    /// RGB shifted by more than <paramref name="toleranceRgb"/> per channel.
    /// </summary>
    BackgroundIntegrityResult CheckBackgroundColor(ImagePixelBuffer before, ImagePixelBuffer after, int toleranceRgb = 10);
}
