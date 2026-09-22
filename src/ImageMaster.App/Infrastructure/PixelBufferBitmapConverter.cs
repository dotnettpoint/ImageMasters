using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Infrastructure;

/// <summary>
/// Converts our library-agnostic <see cref="ImagePixelBuffer"/> to a WPF
/// <see cref="BitmapSource"/> for display. This is the one place the App
/// layer touches WPF imaging types, keeping the conversion out of the
/// view models so they stay unit-testable.
/// </summary>
public static class PixelBufferBitmapConverter
{
    public static BitmapSource ToBitmapSource(ImagePixelBuffer buffer)
    {
        // Our buffer is normalized to Bgra8888/straight-alpha in Infrastructure,
        // which maps directly onto WPF's Pbgra32... except Pbgra32 expects
        // premultiplied alpha, so we use Bgra32 (straight alpha) instead.
        var bitmap = BitmapSource.Create(
            buffer.Width,
            buffer.Height,
            96, 96,
            PixelFormats.Bgra32,
            palette: null,
            buffer.Pixels,
            buffer.Stride);

        bitmap.Freeze(); // Safe to share across threads/bindings once frozen.
        return bitmap;
    }
}
