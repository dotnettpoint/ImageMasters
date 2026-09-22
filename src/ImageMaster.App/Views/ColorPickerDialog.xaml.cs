using System.Windows;
using System.Windows.Media;

namespace ImageMaster.App.Views;

/// <summary>Minimal ARGB color picker (no built-in WPF equivalent exists).</summary>
public partial class ColorPickerDialog : Window
{
    /// <summary>The chosen color, packed as 0xAARRGGBB. Only valid when <see cref="Window.DialogResult"/> is true.</summary>
    public uint SelectedArgb { get; private set; }

    public ColorPickerDialog(uint initialArgb)
    {
        InitializeComponent();

        AlphaSlider.Value = (initialArgb >> 24) & 0xFF;
        RedSlider.Value = (initialArgb >> 16) & 0xFF;
        GreenSlider.Value = (initialArgb >> 8) & 0xFF;
        BlueSlider.Value = initialArgb & 0xFF;

        UpdatePreview();
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (PreviewSwatch is null) return; // Guard against firing during InitializeComponent.

        var color = Color.FromArgb(
            (byte)AlphaSlider.Value,
            (byte)RedSlider.Value,
            (byte)GreenSlider.Value,
            (byte)BlueSlider.Value);

        PreviewSwatch.Background = new SolidColorBrush(color);
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        SelectedArgb =
            ((uint)AlphaSlider.Value << 24) |
            ((uint)RedSlider.Value << 16) |
            ((uint)GreenSlider.Value << 8) |
            (uint)BlueSlider.Value;

        DialogResult = true;
    }
}
