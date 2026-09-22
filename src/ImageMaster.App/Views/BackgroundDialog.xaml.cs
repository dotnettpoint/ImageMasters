using System.Windows;
using Microsoft.Win32;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>Background replacement dialog covering all four <see cref="BackgroundReplaceMode"/> options.</summary>
public partial class BackgroundDialog : Window
{
    private uint _fillArgb = 0xFFFFFFFF; // opaque white
    private uint _keyArgb = 0xFF00FF00;  // green-screen default
    private string? _replacementImagePath;

    public BackgroundReplaceRequest? Result { get; private set; }

    public BackgroundDialog()
    {
        InitializeComponent();
        UpdateSwatches();
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (FillColorPanel is null) return; // still initializing

        var usesFillColor = TransparentToColorRadio.IsChecked == true || ChromaKeyToColorRadio.IsChecked == true;
        var usesImage = TransparentToImageRadio.IsChecked == true || ChromaKeyToImageRadio.IsChecked == true;
        var usesChromaKey = ChromaKeyToColorRadio.IsChecked == true || ChromaKeyToImageRadio.IsChecked == true;

        FillColorPanel.Visibility = usesFillColor ? Visibility.Visible : Visibility.Collapsed;
        ReplacementImagePanel.Visibility = usesImage ? Visibility.Visible : Visibility.Collapsed;
        ChromaKeyPanel.Visibility = usesChromaKey ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnChooseFillColorClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_fillArgb) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _fillArgb = dialog.SelectedArgb;
            UpdateSwatches();
        }
    }

    private void OnChooseKeyColorClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_keyArgb) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _keyArgb = dialog.SelectedArgb;
            UpdateSwatches();
        }
    }

    private void OnBrowseImageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp",
            Title = "Choose Replacement Image"
        };

        if (dialog.ShowDialog() == true)
        {
            _replacementImagePath = dialog.FileName;
            ReplacementImagePathBox.Text = dialog.FileName;
        }
    }

    private void UpdateSwatches()
    {
        FillColorSwatch.Background = new System.Windows.Media.SolidColorBrush(ArgbToColor(_fillArgb));
        KeyColorSwatch.Background = new System.Windows.Media.SolidColorBrush(ArgbToColor(_keyArgb));
    }

    private static System.Windows.Media.Color ArgbToColor(uint argb) => System.Windows.Media.Color.FromArgb(
        (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var mode = TransparentToColorRadio.IsChecked == true ? BackgroundReplaceMode.SolidColorForTransparent
            : TransparentToImageRadio.IsChecked == true ? BackgroundReplaceMode.ImageForTransparent
            : ChromaKeyToColorRadio.IsChecked == true ? BackgroundReplaceMode.ChromaKeyToSolidColor
            : BackgroundReplaceMode.ChromaKeyToImage;

        var request = new BackgroundReplaceRequest
        {
            Mode = mode,
            ArgbFillColor = _fillArgb,
            ReplacementImagePath = _replacementImagePath,
            ChromaKeyArgbColor = _keyArgb,
            ChromaKeyTolerancePercent = (int)ToleranceSlider.Value
        };

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            ValidationText.Text = string.Join(" ", errors);
            return;
        }

        Result = request;
        DialogResult = true;
    }
}
