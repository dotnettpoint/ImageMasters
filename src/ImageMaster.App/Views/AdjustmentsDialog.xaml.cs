using System.Windows;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>Lets the user set brightness/contrast/saturation/sharpen values, applied together as one operation on OK.</summary>
public partial class AdjustmentsDialog : Window
{
    public ImageAdjustmentRequest? Result { get; private set; }

    public AdjustmentsDialog()
    {
        InitializeComponent();
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessText is null) return; // still initializing

        BrightnessText.Text = ((int)BrightnessSlider.Value).ToString();
        ContrastText.Text = ((int)ContrastSlider.Value).ToString();
        SaturationText.Text = ((int)SaturationSlider.Value).ToString();
        SharpenText.Text = ((int)SharpenSlider.Value).ToString();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        BrightnessSlider.Value = 0;
        ContrastSlider.Value = 0;
        SaturationSlider.Value = 0;
        SharpenSlider.Value = 0;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        Result = new ImageAdjustmentRequest
        {
            BrightnessPercent = (int)BrightnessSlider.Value,
            ContrastPercent = (int)ContrastSlider.Value,
            SaturationPercent = (int)SaturationSlider.Value,
            SharpenAmount = (int)SharpenSlider.Value
        };
        DialogResult = true;
    }
}
