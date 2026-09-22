using System.Windows;
using ImageMaster.App.Views;
using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using Microsoft.Win32;

namespace ImageMaster.App.Services;

/// <summary>WPF-backed <see cref="IDialogService"/> implementation. The only place in the app that opens a Window directly.</summary>
public sealed class DialogService : IDialogService
{
    private const string OpenImageFilter =
        "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp|All files|*.*";

    private readonly IImageResizeService _resizeService;

    public DialogService(IImageResizeService resizeService)
    {
        _resizeService = resizeService;
    }

    private static Window? ActiveWindow => System.Windows.Application.Current?.MainWindow;

    public string? ShowOpenImageDialog()
    {
        var dialog = new OpenFileDialog { Filter = OpenImageFilter, Title = "Open Image" };
        return dialog.ShowDialog(ActiveWindow) == true ? dialog.FileName : null;
    }

    public string? ShowOpenFolderDialog()
    {
        var dialog = new OpenFolderDialog { Title = "Select a folder to browse" };
        return dialog.ShowDialog(ActiveWindow) == true ? dialog.FolderName : null;
    }

    public (string Path, ImageFormatType Format)? ShowSaveAsDialog(string suggestedFileName, ImageFormatType suggestedFormat)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save As",
            FileName = suggestedFileName,
            Filter = "JPEG Image|*.jpg|PNG Image|*.png|Bitmap Image|*.bmp|GIF Image|*.gif|TIFF Image|*.tiff|WebP Image|*.webp",
            FilterIndex = FormatToFilterIndex(suggestedFormat),
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(ActiveWindow) != true)
            return null;

        var format = FilterIndexToFormat(dialog.FilterIndex);
        return (dialog.FileName, format);
    }

    public string? ShowOpenReplacementImageDialog()
    {
        var dialog = new OpenFileDialog { Filter = OpenImageFilter, Title = "Choose Replacement Image" };
        return dialog.ShowDialog(ActiveWindow) == true ? dialog.FileName : null;
    }

    public uint? ShowColorPickerDialog(uint initialArgb)
    {
        var dialog = new ColorPickerDialog(initialArgb) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.SelectedArgb : null;
    }

    public bool ShowConfirm(string title, string message) =>
        MessageBox.Show(ActiveWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        MessageBox.Show(ActiveWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message) =>
        MessageBox.Show(ActiveWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void CopyToClipboard(string text) => Clipboard.SetText(text);

    public ResizeRequest? ShowResizeDialog(int sourceWidth, int sourceHeight)
    {
        var dialog = new ResizeDialog(sourceWidth, sourceHeight, _resizeService) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public ImageAdjustmentRequest? ShowAdjustmentsDialog()
    {
        var dialog = new AdjustmentsDialog { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public CropMode? ShowCropConfirmDialog()
    {
        var dialog = new CropConfirmDialog { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public int? ShowQualityDialog(int currentQuality)
    {
        var dialog = new QualityDialog(currentQuality) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public DpiChangeRequest? ShowDpiDialog(double currentDpiX, double currentDpiY)
    {
        var dialog = new DpiDialog(currentDpiX, currentDpiY) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public IReadOnlyList<TextOverlayLayer>? ShowTextOverlayDialog(IReadOnlyList<TextOverlayLayer> existingLayers, int imageWidth, int imageHeight, uint? defaultNewLayerColor = null)
    {
        var dialog = new TextOverlayDialog(existingLayers, imageWidth, imageHeight, defaultNewLayerColor) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public BackgroundReplaceRequest? ShowBackgroundDialog(uint? initialFillColor = null)
    {
        var dialog = new BackgroundDialog(initialFillColor) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private static int FormatToFilterIndex(ImageFormatType format) => format switch
    {
        ImageFormatType.Jpeg => 1,
        ImageFormatType.Png => 2,
        ImageFormatType.Bmp => 3,
        ImageFormatType.Gif => 4,
        ImageFormatType.Tiff => 5,
        ImageFormatType.WebP => 6,
        _ => 2
    };

    private static ImageFormatType FilterIndexToFormat(int filterIndex) => filterIndex switch
    {
        1 => ImageFormatType.Jpeg,
        2 => ImageFormatType.Png,
        3 => ImageFormatType.Bmp,
        4 => ImageFormatType.Gif,
        5 => ImageFormatType.Tiff,
        6 => ImageFormatType.WebP,
        _ => ImageFormatType.Png
    };
}
