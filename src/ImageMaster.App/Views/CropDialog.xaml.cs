using System.Windows;
using System.Windows.Controls;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>Crop dialog: lets the user enter a pixel rectangle (X/Y/Width/Height) to extract from the image.</summary>
public partial class CropDialog : Window
{
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;

    public CropRequest? Result { get; private set; }

    public CropDialog(int sourceWidth, int sourceHeight)
    {
        InitializeComponent();
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;

        SourceSizeText.Text = $"Current size: {sourceWidth} x {sourceHeight} px";
        XBox.Text = "0";
        YBox.Text = "0";
        WidthBox.Text = sourceWidth.ToString();
        HeightBox.Text = sourceHeight.ToString();

        UpdatePreview();
    }

    private void OnFieldChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        XBox.Text = "0";
        YBox.Text = "0";
        WidthBox.Text = _sourceWidth.ToString();
        HeightBox.Text = _sourceHeight.ToString();
    }

    private void UpdatePreview()
    {
        if (PreviewText is null) return; // still initializing

        var request = BuildRequest(out var errors);
        if (request is null)
        {
            PreviewText.Text = "Result: -";
            ValidationText.Text = string.Join(" ", errors);
            return;
        }

        PreviewText.Text = $"Result: {request.Width} x {request.Height} px, starting at ({request.X}, {request.Y})";
        ValidationText.Text = string.Empty;
    }

    private CropRequest? BuildRequest(out IReadOnlyList<string> validationErrors)
    {
        if (!int.TryParse(XBox.Text, out var x) || !int.TryParse(YBox.Text, out var y) ||
            !int.TryParse(WidthBox.Text, out var width) || !int.TryParse(HeightBox.Text, out var height))
        {
            validationErrors = new List<string> { "All fields must be whole numbers." };
            return null;
        }

        var request = new CropRequest { X = x, Y = y, Width = width, Height = height };
        var errors = request.Validate(_sourceWidth, _sourceHeight);
        validationErrors = errors;
        return errors.Count == 0 ? request : null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var request = BuildRequest(out var errors);
        if (request is null)
        {
            ValidationText.Text = string.Join(" ", errors);
            return;
        }

        Result = request;
        DialogResult = true;
    }
}
