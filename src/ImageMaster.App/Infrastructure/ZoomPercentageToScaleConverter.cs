using System.Globalization;
using System.Windows.Data;

namespace ImageMaster.App.Infrastructure;

/// <summary>Converts a zoom percentage (25-400) to a WPF <c>ScaleTransform</c> factor (0.25-4.0).</summary>
public sealed class ZoomPercentageToScaleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double percentage ? percentage / 100.0 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double scale ? scale * 100.0 : 100.0;
}
