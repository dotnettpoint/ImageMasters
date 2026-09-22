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

    /// <summary>Shows the resize dialog and returns the user's chosen request, or null if cancelled.</summary>
    ResizeRequest? ShowResizeDialog(int sourceWidth, int sourceHeight);

    /// <summary>Shows the DPI dialog and returns the user's chosen request, or null if cancelled.</summary>
    DpiChangeRequest? ShowDpiDialog(double currentDpiX, double currentDpiY);

    /// <summary>Shows the text overlay editor and returns the finished layer list (possibly unchanged), or null if cancelled.</summary>
    IReadOnlyList<TextOverlayLayer>? ShowTextOverlayDialog(IReadOnlyList<TextOverlayLayer> existingLayers, int imageWidth, int imageHeight);

    /// <summary>Shows the background-replace dialog and returns the user's chosen request, or null if cancelled.</summary>
    BackgroundReplaceRequest? ShowBackgroundDialog();
}
