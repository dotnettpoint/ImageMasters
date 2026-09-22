using System.Windows;

namespace ImageMaster.App.Views;

/// <summary>Lets the user pick a JPEG/WebP export quality (1-100) before saving.</summary>
public partial class QualityDialog : Window
{
    public int Result { get; private set; }

    public QualityDialog(int currentQuality)
    {
        InitializeComponent();
        QualitySlider.Value = Math.Clamp(currentQuality, 1, 100);
    }

    private void OnQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityValueText is null) return; // still initializing
        QualityValueText.Text = ((int)Math.Round(QualitySlider.Value)).ToString();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        Result = (int)Math.Round(QualitySlider.Value);
        DialogResult = true;
    }
}
