using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMaster.App.Infrastructure;
using ImageMaster.App.Services;
using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;

namespace ImageMaster.App.ViewModels;

/// <summary>
/// The main window's view model: owns the open document, drives every
/// editing operation, and exposes everything the view binds to. Kept free
/// of WPF Window/dialog types by going through <see cref="IDialogService"/>.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IImageLoaderService _loaderService;
    private readonly IImageResizeService _resizeService;
    private readonly IDpiService _dpiService;
    private readonly ITextOverlayService _textOverlayService;
    private readonly IBackgroundService _backgroundService;
    private readonly IFileExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly IAppLogger _logger;

    private readonly UndoRedoStack<EditorSnapshot> _undoRedo = new();

    public ObservableCollection<ThumbnailItemViewModel> Thumbnails { get; } = new();

    public MainViewModel(
        IImageLoaderService loaderService,
        IImageResizeService resizeService,
        IDpiService dpiService,
        ITextOverlayService textOverlayService,
        IBackgroundService backgroundService,
        IFileExportService exportService,
        IDialogService dialogService,
        IAppLogger logger)
    {
        _loaderService = loaderService;
        _resizeService = resizeService;
        _dpiService = dpiService;
        _textOverlayService = textOverlayService;
        _backgroundService = backgroundService;
        _exportService = exportService;
        _dialogService = dialogService;
        _logger = logger;

        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync, () => Document is not null);
        ResizeCommand = new AsyncRelayCommand(ResizeAsync, () => Document is not null);
        AddTextCommand = new AsyncRelayCommand(EditTextLayersAsync, () => Document is not null);
        ApplyTextCommand = new AsyncRelayCommand(ApplyTextLayersAsync, () => Document is { TextLayers.Count: > 0 });
        BackgroundCommand = new AsyncRelayCommand(ChangeBackgroundAsync, () => Document is not null);
        DpiCommand = new RelayCommand(ChangeDpi, () => Document is not null);
        UndoCommand = new RelayCommand(Undo, () => _undoRedo.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _undoRedo.CanRedo);
        SelectThumbnailCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedThumbnail is not null)
                await LoadFileAsync(SelectedThumbnail.FilePath);
        });
    }

    // --- Bindable state -----------------------------------------------

    private ImageDocument? _document;
    public ImageDocument? Document
    {
        get => _document;
        private set
        {
            if (SetProperty(ref _document, value))
            {
                OnPropertyChanged(nameof(HasDocument));
                OnPropertyChanged(nameof(WindowTitle));
                RaiseAllCommandsCanExecuteChanged();
            }
        }
    }

    public bool HasDocument => Document is not null;

    public string WindowTitle => Document is null
        ? "ImageMaster"
        : $"ImageMaster - {Path.GetFileName(Document.SourceFilePath)}{(Document.HasUnsavedChanges ? " *" : "")}";

    private BitmapSource? _currentImageSource;
    public BitmapSource? CurrentImageSource
    {
        get => _currentImageSource;
        private set => SetProperty(ref _currentImageSource, value);
    }

    private double _zoomPercentage = 100;
    public double ZoomPercentage
    {
        get => _zoomPercentage;
        set => SetProperty(ref _zoomPercentage, Math.Clamp(value, 25, 400));
    }

    private string _statusText = "Open an image to get started.";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                RaiseAllCommandsCanExecuteChanged();
        }
    }

    private ThumbnailItemViewModel? _selectedThumbnail;
    public ThumbnailItemViewModel? SelectedThumbnail
    {
        get => _selectedThumbnail;
        set => SetProperty(ref _selectedThumbnail, value);
    }

    public bool CanUndo => _undoRedo.CanUndo;
    public bool CanRedo => _undoRedo.CanRedo;

    // --- Commands --------------------------------------------------------

    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand SaveAsCommand { get; }
    public AsyncRelayCommand ResizeCommand { get; }
    public AsyncRelayCommand AddTextCommand { get; }
    public AsyncRelayCommand ApplyTextCommand { get; }
    public AsyncRelayCommand BackgroundCommand { get; }
    public RelayCommand DpiCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public AsyncRelayCommand SelectThumbnailCommand { get; }

    // --- File operations ---------------------------------------------

    private async Task OpenAsync()
    {
        var filePath = _dialogService.ShowOpenImageDialog();
        if (filePath is null) return;

        await LoadFileAsync(filePath);
    }

    private async Task LoadFileAsync(string filePath)
    {
        IsBusy = true;
        StatusText = $"Opening {Path.GetFileName(filePath)}...";

        try
        {
            var result = await _loaderService.LoadAsync(filePath);
            if (!result.Success)
            {
                _dialogService.ShowError("Couldn't Open Image", result.ErrorMessage!);
                StatusText = "Ready.";
                return;
            }

            Document = result.Value;
            _undoRedo.Clear();
            RefreshCurrentImageSource();
            UpdateStatusText();

            if (result.Warnings.Count > 0)
                StatusText += $"  (Opened with {result.Warnings.Count} metadata warning(s) - see log.)";

            await PopulateThumbnailStripAsync(filePath);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PopulateThumbnailStripAsync(string currentFilePath)
    {
        Thumbnails.Clear();

        var folder = Path.GetDirectoryName(currentFilePath);
        if (folder is null || !Directory.Exists(folder)) return;

        var siblingFiles = Directory.EnumerateFiles(folder)
            .Where(f => _loaderService.SupportedExtensions.Contains(Path.GetExtension(f).TrimStart('.')))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in siblingFiles)
        {
            var item = new ThumbnailItemViewModel(file) { IsSelected = string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase) };
            Thumbnails.Add(item);
            if (item.IsSelected) SelectedThumbnail = item;
        }

        // Load thumbnail images in the background so opening a folder full
        // of large images doesn't stall the UI.
        foreach (var item in Thumbnails.ToList())
        {
            var thumbResult = await _loaderService.LoadThumbnailAsync(item.FilePath, 96);
            if (thumbResult.Success)
                item.Thumbnail = PixelBufferBitmapConverter.ToBitmapSource(thumbResult.Value!);
        }
    }

    private async Task SaveAsAsync()
    {
        if (Document is null) return;

        var suggestedName = Path.GetFileNameWithoutExtension(Document.SourceFilePath);
        var choice = _dialogService.ShowSaveAsDialog(suggestedName, Document.OriginalFormat);
        if (choice is null) return;

        var (outputPath, format) = choice.Value;

        var overwritesOriginal = string.Equals(
            Path.GetFullPath(outputPath), Path.GetFullPath(Document.SourceFilePath), StringComparison.OrdinalIgnoreCase);

        if (overwritesOriginal)
        {
            var confirmed = _dialogService.ShowConfirm(
                "Overwrite Original?",
                "This will overwrite the original file you opened. This cannot be undone. Continue?");
            if (!confirmed) return;
        }

        var quality = format is ImageFormatType.Jpeg or ImageFormatType.WebP
            ? PromptForQualityOrDefault()
            : 90;

        IsBusy = true;
        StatusText = "Saving...";
        try
        {
            // Flatten any pending text layers before export, so what's saved matches what's shown.
            var bufferToSave = Document.TextLayers.Count > 0
                ? await FlattenTextLayersAsync(Document.PixelBuffer, Document.TextLayers)
                : Document.PixelBuffer;

            if (bufferToSave is null) return; // flatten failed; error already shown

            var request = new ExportRequest
            {
                OutputFilePath = outputPath,
                TargetFormat = format,
                JpegQuality = quality,
                PreserveMetadata = true,
                AllowOverwriteOriginal = overwritesOriginal
            };

            var result = await _exportService.SaveAsAsync(bufferToSave, Document.Metadata, Document.SourceFilePath, request);
            if (!result.Success)
            {
                _dialogService.ShowError("Couldn't Save Image", result.ErrorMessage!);
                return;
            }

            Document.HasUnsavedChanges = false;
            OnPropertyChanged(nameof(WindowTitle));
            StatusText = $"Saved to {result.Value}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private int PromptForQualityOrDefault() => 90; // A dedicated quality-slider dialog could replace this; 90 is a sensible high-quality default.

    // --- Editing operations -------------------------------------------

    private async Task ResizeAsync()
    {
        if (Document is null) return;

        var request = _dialogService.ShowResizeDialog(Document.PixelBuffer.Width, Document.PixelBuffer.Height);
        if (request is null) return;

        await RunEditAsync("Resizing...", async () =>
        {
            var result = await _resizeService.ResizeAsync(Document.PixelBuffer, request);
            if (!result.Success)
            {
                _dialogService.ShowError("Resize Failed", result.ErrorMessage!);
                return false;
            }

            Document.PixelBuffer = result.Value!;
            return true;
        });
    }

    private async Task EditTextLayersAsync()
    {
        if (Document is null) return;

        var layers = _dialogService.ShowTextOverlayDialog(Document.TextLayers, Document.PixelBuffer.Width, Document.PixelBuffer.Height);
        if (layers is null) return;

        PushUndoSnapshot();
        Document.TextLayers.Clear();
        Document.TextLayers.AddRange(layers);
        Document.HasUnsavedChanges = true;
        OnPropertyChanged(nameof(WindowTitle));
        // Document's reference didn't change, but its text layers did - notify
        // explicitly so the view's canvas overlay (which only re-renders on
        // Document/CurrentImageSource change notifications) picks it up.
        OnPropertyChanged(nameof(Document));
        RaiseAllCommandsCanExecuteChanged();
        StatusText = $"{Document.TextLayers.Count} text layer(s) pending - use Apply Text to flatten, or Save to flatten automatically.";
    }

    private async Task ApplyTextLayersAsync()
    {
        if (Document is null || Document.TextLayers.Count == 0) return;

        await RunEditAsync("Applying text...", async () =>
        {
            var flattened = await FlattenTextLayersAsync(Document.PixelBuffer, Document.TextLayers);
            if (flattened is null) return false;

            Document.PixelBuffer = flattened;
            Document.TextLayers.Clear();
            return true;
        });
    }

    private async Task<ImagePixelBuffer?> FlattenTextLayersAsync(ImagePixelBuffer source, IReadOnlyList<TextOverlayLayer> layers)
    {
        var result = await _textOverlayService.ApplyTextLayersAsync(source, layers);
        if (!result.Success)
        {
            _dialogService.ShowError("Text Overlay Failed", result.ErrorMessage!);
            return null;
        }
        return result.Value;
    }

    private async Task ChangeBackgroundAsync()
    {
        if (Document is null) return;

        var request = _dialogService.ShowBackgroundDialog();
        if (request is null) return;

        await RunEditAsync("Changing background...", async () =>
        {
            var result = await _backgroundService.ReplaceBackgroundAsync(Document.PixelBuffer, request);
            if (!result.Success)
            {
                _dialogService.ShowError("Background Change Failed", result.ErrorMessage!);
                return false;
            }

            Document.PixelBuffer = result.Value!;
            return true;
        });
    }

    private void ChangeDpi()
    {
        if (Document is null) return;

        var request = _dialogService.ShowDpiDialog(Document.Metadata.DpiX, Document.Metadata.DpiY);
        if (request is null) return;

        var result = _dpiService.ChangeDpi(Document.Metadata, request);
        if (!result.Success)
        {
            _dialogService.ShowError("Couldn't Change DPI", result.ErrorMessage!);
            return;
        }

        PushUndoSnapshot();
        Document.Metadata = result.Value!;
        Document.HasUnsavedChanges = true;
        OnPropertyChanged(nameof(WindowTitle));
        StatusText = $"DPI set to {request.DpiX:0.#} x {request.DpiY:0.#}.";
    }

    // --- Undo/redo -------------------------------------------------------

    private void PushUndoSnapshot()
    {
        if (Document is null) return;
        _undoRedo.Push(EditorSnapshot.CaptureFrom(Document));
        RaiseAllCommandsCanExecuteChanged();
    }

    private void Undo()
    {
        if (Document is null || !_undoRedo.CanUndo) return;

        var current = EditorSnapshot.CaptureFrom(Document);
        var previous = _undoRedo.Undo(current);
        ApplySnapshot(previous);
    }

    private void Redo()
    {
        if (Document is null || !_undoRedo.CanRedo) return;

        var current = EditorSnapshot.CaptureFrom(Document);
        var next = _undoRedo.Redo(current);
        ApplySnapshot(next);
    }

    private void ApplySnapshot(EditorSnapshot snapshot)
    {
        if (Document is null) return;

        Document.PixelBuffer = snapshot.PixelBuffer;
        Document.TextLayers.Clear();
        Document.TextLayers.AddRange(snapshot.TextLayers);
        Document.HasUnsavedChanges = true;

        RefreshCurrentImageSource();
        UpdateStatusText();
        OnPropertyChanged(nameof(WindowTitle));
        RaiseAllCommandsCanExecuteChanged();
    }

    // --- Shared helpers ---------------------------------------------

    /// <summary>Runs a destructive pixel edit: snapshots for undo (only once it actually succeeds), executes, refreshes the display, and reports errors uniformly.</summary>
    private async Task RunEditAsync(string busyText, Func<Task<bool>> editAction)
    {
        if (Document is null) return;

        IsBusy = true;
        StatusText = busyText;
        try
        {
            // Capture the pre-edit state without touching the undo stack yet -
            // editAction only mutates Document.PixelBuffer on success, so if it
            // fails there is nothing to roll back and nothing should be pushed.
            var preEditSnapshot = EditorSnapshot.CaptureFrom(Document);

            var succeeded = await editAction();
            if (!succeeded)
            {
                StatusText = "Ready.";
                return;
            }

            _undoRedo.Push(preEditSnapshot);
            RaiseAllCommandsCanExecuteChanged();

            Document.HasUnsavedChanges = true;
            RefreshCurrentImageSource();
            UpdateStatusText();
            OnPropertyChanged(nameof(WindowTitle));
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error during edit operation.", ex);
            _dialogService.ShowError("Unexpected Error", "Something went wrong performing that edit. Details were written to the log.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCurrentImageSource()
    {
        CurrentImageSource = Document is null ? null : PixelBufferBitmapConverter.ToBitmapSource(Document.PixelBuffer);
    }

    private void UpdateStatusText()
    {
        if (Document is null)
        {
            StatusText = "Open an image to get started.";
            return;
        }

        StatusText = $"{Document.PixelBuffer.Width} x {Document.PixelBuffer.Height} px, " +
                      $"{Document.Metadata.DpiX:0.#} dpi, {Path.GetFileName(Document.SourceFilePath)}";
    }

    private void RaiseAllCommandsCanExecuteChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
