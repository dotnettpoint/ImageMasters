using ImageMaster.Core.Models;

namespace ImageMaster.App.Services;

/// <summary>
/// Abstracts WPF's file/dialog APIs behind an interface so view models never
/// touch <c>System.Windows</c> dialog types directly, keeping them testable.
/// </summary>
public interface IDialogService
{
    string? ShowOpenImageDialog();
    string? ShowOpenFolderDialog();
    (string Path, ImageFormatType Format)? ShowSaveAsDialog(string suggestedFileName, ImageFormatType suggestedFormat);
    string? ShowOpenReplacementImageDialog();
    uint? ShowColorPickerDialog(uint initialArgb);
    bool ShowConfirm(string title, string message);
    void ShowError(string title, string message);
    void ShowInfo(string title, string message);

    /// <summary>Copies text (e.g. a picked color's hex value) to the system clipboard.</summary>
    void CopyToClipboard(string text);

    /// <summary>Shows the resize dialog and returns the user's chosen request, or null if cancelled.</summary>
    ResizeRequest? ShowResizeDialog(int sourceWidth, int sourceHeight);

    /// <summary>Shows the post-selection crop prompt (extract as new file vs. replace the working image), or null if cancelled.</summary>
    CropMode? ShowCropConfirmDialog();

    /// <summary>Shows the JPEG/WebP quality dialog and returns the chosen quality (1-100), or null if cancelled.</summary>
    int? ShowQualityDialog(int currentQuality);

    /// <summary>Shows the DPI dialog and returns the user's chosen request, or null if cancelled.</summary>
    DpiChangeRequest? ShowDpiDialog(double currentDpiX, double currentDpiY);

    /// <summary>Shows the text overlay editor and returns the finished layer list (possibly unchanged), or null if cancelled.</summary>
    /// <param name="defaultNewLayerColor">ARGB color to use for newly-added layers (e.g. from the eyedropper), or null for the built-in default.</param>
    IReadOnlyList<TextOverlayLayer>? ShowTextOverlayDialog(IReadOnlyList<TextOverlayLayer> existingLayers, int imageWidth, int imageHeight, uint? defaultNewLayerColor = null);

    /// <summary>Shows the background-replace dialog and returns the user's chosen request, or null if cancelled.</summary>
    /// <param name="initialFillColor">ARGB color to pre-select as the fill color (e.g. from the eyedropper), or null for the built-in default.</param>
    BackgroundReplaceRequest? ShowBackgroundDialog(uint? initialFillColor = null);
}
