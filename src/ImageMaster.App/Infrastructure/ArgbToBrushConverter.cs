using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ImageMaster.App.Infrastructure;

/// <summary>Converts a nullable packed ARGB <c>uint</c> (as used by the picked-color swatch) to a WPF brush.</summary>
public sealed class ArgbToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not uint argb) return Brushes.Transparent;

        var color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
