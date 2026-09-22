using System.Windows;
using System.Windows.Controls;
using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>
/// Resize dialog: lets the user resize by target pixel dimensions (with an
/// aspect-lock toggle) or by percentage, and shows a live preview of the
/// resulting size before applying.
/// </summary>
public partial class ResizeDialog : Window
{
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly IImageResizeService _resizeService;
    private bool _isUpdatingFromCode;

    public ResizeRequest? Result { get; private set; }

    public ResizeDialog(int sourceWidth, int sourceHeight, IImageResizeService resizeService)
    {
        InitializeComponent();
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _resizeService = resizeService;

        SourceSizeText.Text = $"Current size: {sourceWidth} x {sourceHeight} px";

        _isUpdatingFromCode = true;
        WidthBox.Text = sourceWidth.ToString();
        HeightBox.Text = sourceHeight.ToString();
        _isUpdatingFromCode = false;

        UpdatePreview();
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (DimensionsPanel is null || PercentagePanel is null) return; // still initializing

        DimensionsPanel.Visibility = ByDimensionsRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AspectLockCheckBox.Visibility = ByDimensionsRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PercentagePanel.Visibility = ByPercentageRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }

    private void OnWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFromCode) return;

        if (AspectLockCheckBox.IsChecked == true && int.TryParse(WidthBox.Text, out var width) && width > 0)
        {
            var height = (int)Math.Round(width * ((double)_sourceHeight / _sourceWidth));
            _isUpdatingFromCode = true;
            HeightBox.Text = height.ToString();
            _isUpdatingFromCode = false;
        }

        UpdatePreview();
    }

    private void OnHeightChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFromCode) return;

        if (AspectLockCheckBox.IsChecked == true && int.TryParse(HeightBox.Text, out var height) && height > 0)
        {
            var width = (int)Math.Round(height * ((double)_sourceWidth / _sourceHeight));
            _isUpdatingFromCode = true;
            WidthBox.Text = width.ToString();
            _isUpdatingFromCode = false;
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewText is null) return;

        var request = BuildRequest(out var validationErrors);
        if (request is null)
        {
            PreviewText.Text = "Result: -";
            ValidationText.Text = string.Join(" ", validationErrors);
            return;
        }

        var (width, height) = _resizeService.CalculateTargetDimensions(_sourceWidth, _sourceHeight, request);
        PreviewText.Text = $"Result: {width} x {height} px";
        ValidationText.Text = string.Empty;
    }

    private ResizeRequest? BuildRequest(out IReadOnlyList<string> validationErrors)
    {
        var byPercentage = ByPercentageRadio.IsChecked == true;

        ResizeRequest request;
        if (byPercentage)
        {
            double? percent = double.TryParse(PercentageBox.Text, out var p) ? p : null;
            request = new ResizeRequest
            {
                ByPercentage = true,
                PercentageValue = percent,
                MaintainAspectRatio = true,
                PreserveMetadata = PreserveMetadataCheckBox.IsChecked == true
            };
        }
        else
        {
            int? width = int.TryParse(WidthBox.Text, out var w) ? w : null;
            int? height = int.TryParse(HeightBox.Text, out var h) ? h : null;
            request = new ResizeRequest
            {
                ByPercentage = false,
                TargetWidth = width,
                TargetHeight = height,
                MaintainAspectRatio = AspectLockCheckBox.IsChecked == true,
                PreserveMetadata = PreserveMetadataCheckBox.IsChecked == true
            };
        }

        var errors = request.Validate();
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
