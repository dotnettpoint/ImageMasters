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
    private readonly IImageTransformService _transformService;
    private readonly IImageCropService _cropService;
    private readonly IImageIntegrityService _integrityService;
    private readonly IImageAdjustmentService _adjustmentService;
    private readonly IImageFilterService _filterService;
    private readonly IBackgroundSegmentationService _segmentationService;
    private readonly IDpiService _dpiService;
    private readonly ITextOverlayService _textOverlayService;
    private readonly IBackgroundService _backgroundService;
    private readonly IFileExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly IAppLogger _logger;

    private readonly UndoRedoStack<EditorSnapshot> _undoRedo = new();

    /// <summary>How many thumbnails are decoded and shown at once. Kept small so a folder with hundreds of photos never stalls the UI decoding all of them up front.</summary>
    private const int ThumbnailPageSize = 24;

    /// <summary>All image file paths in the currently open folder (cheap to enumerate - no decoding). Drives both thumbnail paging and Previous/Next image navigation.</summary>
    private List<string> _folderFiles = new();
    private string? _folderPath;

    /// <summary>Remembers the last JPEG/WebP quality the user picked, so a quick Save doesn't have to prompt again.</summary>
    private int _lastUsedQuality = 90;

    public ObservableCollection<ThumbnailItemViewModel> Thumbnails { get; } = new();

    public MainViewModel(
        IImageLoaderService loaderService,
        IImageResizeService resizeService,
        IImageTransformService transformService,
        IImageCropService cropService,
        IImageIntegrityService integrityService,
        IImageAdjustmentService adjustmentService,
        IImageFilterService filterService,
        IBackgroundSegmentationService segmentationService,
        IDpiService dpiService,
        ITextOverlayService textOverlayService,
        IBackgroundService backgroundService,
        IFileExportService exportService,
        IDialogService dialogService,
        IAppLogger logger)
    {
        _loaderService = loaderService;
        _resizeService = resizeService;
        _transformService = transformService;
        _cropService = cropService;
        _integrityService = integrityService;
        _adjustmentService = adjustmentService;
        _filterService = filterService;
        _segmentationService = segmentationService;
        _dpiService = dpiService;
        _textOverlayService = textOverlayService;
        _backgroundService = backgroundService;
        _exportService = exportService;
        _dialogService = dialogService;
        _logger = logger;

        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => Document is not null);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync, () => Document is not null);
        RotateClockwiseCommand = new AsyncRelayCommand(() => RotateAsync(RotateDirection.Clockwise90), () => Document is not null);
        RotateCounterClockwiseCommand = new AsyncRelayCommand(() => RotateAsync(RotateDirection.CounterClockwise90), () => Document is not null);
        PreviousImageCommand = new AsyncRelayCommand(() => NavigateAsync(-1), () => CanNavigate(-1));
        NextImageCommand = new AsyncRelayCommand(() => NavigateAsync(1), () => CanNavigate(1));
        PreviousThumbnailPageCommand = new AsyncRelayCommand(() => LoadThumbnailPageAsync(ThumbnailPageIndex - 1), () => ThumbnailPageIndex > 0);
        NextThumbnailPageCommand = new AsyncRelayCommand(() => LoadThumbnailPageAsync(ThumbnailPageIndex + 1), () => ThumbnailPageIndex < ThumbnailPageCount - 1);
        ResizeCommand = new AsyncRelayCommand(ResizeAsync, () => Document is not null);
        CropCommand = new RelayCommand(() => CurrentTool = CurrentTool == ToolMode.Crop ? ToolMode.None : ToolMode.Crop, () => Document is not null);
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
        EyedropperToolCommand = new RelayCommand(() => CurrentTool = CurrentTool == ToolMode.Eyedropper ? ToolMode.None : ToolMode.Eyedropper, () => Document is not null);
        CopyPickedColorCommand = new RelayCommand(() => _dialogService.CopyToClipboard(PickedColorHex), () => PickedColorArgb is not null);
        AdjustmentsCommand = new AsyncRelayCommand(AdjustAsync, () => Document is not null);
        GrayscaleFilterCommand = new AsyncRelayCommand(() => ApplyFilterAsync(ImageFilterType.Grayscale), () => Document is not null);
        SepiaFilterCommand = new AsyncRelayCommand(() => ApplyFilterAsync(ImageFilterType.Sepia), () => Document is not null);
        InvertFilterCommand = new AsyncRelayCommand(() => ApplyFilterAsync(ImageFilterType.Invert), () => Document is not null);
        AiRemoveBackgroundCommand = new AsyncRelayCommand(AiRemoveBackgroundAsync, () => Document is not null);
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

    private int _thumbnailPageIndex;
    public int ThumbnailPageIndex
    {
        get => _thumbnailPageIndex;
        private set => SetProperty(ref _thumbnailPageIndex, value);
    }

    public int ThumbnailPageCount => _folderFiles.Count == 0
        ? 0
        : (int)Math.Ceiling(_folderFiles.Count / (double)ThumbnailPageSize);

    public string ThumbnailPageLabel => _folderFiles.Count == 0
        ? string.Empty
        : $"Page {ThumbnailPageIndex + 1} of {ThumbnailPageCount} ({_folderFiles.Count} photos)";

    public bool CanUndo => _undoRedo.CanUndo;
    public bool CanRedo => _undoRedo.CanRedo;

    private ToolMode _currentTool = ToolMode.None;
    /// <summary>Which interactive canvas tool is active. Only one tool is active at a time - activating one deactivates any other.</summary>
    public ToolMode CurrentTool
    {
        get => _currentTool;
        set
        {
            if (SetProperty(ref _currentTool, value))
            {
                OnPropertyChanged(nameof(IsEyedropperActive));
                OnPropertyChanged(nameof(IsCropToolActive));
            }
        }
    }

    public bool IsEyedropperActive => CurrentTool == ToolMode.Eyedropper;
    public bool IsCropToolActive => CurrentTool == ToolMode.Crop;

    private uint? _pickedColorArgb;
    /// <summary>The last color sampled with the eyedropper tool, if any.</summary>
    public uint? PickedColorArgb
    {
        get => _pickedColorArgb;
        private set
        {
            if (SetProperty(ref _pickedColorArgb, value))
            {
                OnPropertyChanged(nameof(PickedColorHex));
                RaiseAllCommandsCanExecuteChanged();
            }
        }
    }

    public string PickedColorHex => PickedColorArgb is uint argb
        ? $"#{(argb >> 16) & 0xFF:X2}{(argb >> 8) & 0xFF:X2}{argb & 0xFF:X2}"
        : "No color picked";

    private bool _isShowingOriginal;
    /// <summary>
    /// Before/After comparison: while true, the display shows the image
    /// exactly as it was when opened instead of the current (edited) buffer.
    /// Meant to be driven by a press-and-hold "Compare" button.
    /// </summary>
    public bool IsShowingOriginal
    {
        get => _isShowingOriginal;
        set
        {
            if (SetProperty(ref _isShowingOriginal, value))
                RefreshCurrentImageSource();
        }
    }

    // --- Commands --------------------------------------------------------

    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand SaveAsCommand { get; }
    public AsyncRelayCommand RotateClockwiseCommand { get; }
    public AsyncRelayCommand RotateCounterClockwiseCommand { get; }
    public AsyncRelayCommand PreviousImageCommand { get; }
    public AsyncRelayCommand NextImageCommand { get; }
    public AsyncRelayCommand PreviousThumbnailPageCommand { get; }
    public AsyncRelayCommand NextThumbnailPageCommand { get; }
    public AsyncRelayCommand ResizeCommand { get; }
    public RelayCommand CropCommand { get; }
    public AsyncRelayCommand AddTextCommand { get; }
    public AsyncRelayCommand ApplyTextCommand { get; }
    public AsyncRelayCommand BackgroundCommand { get; }
    public RelayCommand DpiCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public AsyncRelayCommand SelectThumbnailCommand { get; }
    public RelayCommand EyedropperToolCommand { get; }
    public RelayCommand CopyPickedColorCommand { get; }
    public AsyncRelayCommand AdjustmentsCommand { get; }
    public AsyncRelayCommand GrayscaleFilterCommand { get; }
    public AsyncRelayCommand SepiaFilterCommand { get; }
    public AsyncRelayCommand InvertFilterCommand { get; }
    public AsyncRelayCommand AiRemoveBackgroundCommand { get; }

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
            IsShowingOriginal = false;
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

    /// <summary>
    /// Re-scans the current folder's file list only when the folder actually
    /// changed (cheap - no decoding), then loads whichever thumbnail page
    /// contains <paramref name="currentFilePath"/>.
    /// </summary>
    private async Task PopulateThumbnailStripAsync(string currentFilePath)
    {
        var folder = Path.GetDirectoryName(currentFilePath);
        if (folder is null || !Directory.Exists(folder))
        {
            _folderFiles = new List<string>();
            _folderPath = null;
            await LoadThumbnailPageAsync(0, currentFilePath);
            return;
        }

        if (!string.Equals(folder, _folderPath, StringComparison.OrdinalIgnoreCase))
        {
            _folderPath = folder;
            _folderFiles = Directory.EnumerateFiles(folder)
                .Where(f => _loaderService.SupportedExtensions.Contains(Path.GetExtension(f).TrimStart('.')))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var currentIndex = _folderFiles.FindIndex(f => string.Equals(f, currentFilePath, StringComparison.OrdinalIgnoreCase));
        var pageIndex = currentIndex < 0 ? 0 : currentIndex / ThumbnailPageSize;

        await LoadThumbnailPageAsync(pageIndex, currentFilePath);
    }

    /// <summary>
    /// Loads (decodes) thumbnails for just one page of the current folder's
    /// files - this is the actual expensive step, so folders with hundreds of
    /// photos never decode more than <see cref="ThumbnailPageSize"/> at once.
    /// If the requested page is already the one showing, this only moves the
    /// selection highlight instead of re-decoding anything.
    /// </summary>
    private async Task LoadThumbnailPageAsync(int pageIndex, string? currentFilePath = null)
    {
        if (_folderFiles.Count == 0)
        {
            Thumbnails.Clear();
            ThumbnailPageIndex = 0;
            SelectedThumbnail = null;
            OnPropertyChanged(nameof(ThumbnailPageCount));
            OnPropertyChanged(nameof(ThumbnailPageLabel));
            RaiseAllCommandsCanExecuteChanged();
            return;
        }

        pageIndex = Math.Clamp(pageIndex, 0, ThumbnailPageCount - 1);
        var targetPath = currentFilePath ?? Document?.SourceFilePath;

        if (pageIndex == ThumbnailPageIndex && Thumbnails.Count > 0)
        {
            foreach (var item in Thumbnails)
            {
                item.IsSelected = string.Equals(item.FilePath, targetPath, StringComparison.OrdinalIgnoreCase);
                if (item.IsSelected) SelectedThumbnail = item;
            }
            return;
        }

        ThumbnailPageIndex = pageIndex;
        Thumbnails.Clear();

        var pageFiles = _folderFiles.Skip(pageIndex * ThumbnailPageSize).Take(ThumbnailPageSize).ToList();
        foreach (var file in pageFiles)
        {
            var item = new ThumbnailItemViewModel(file) { IsSelected = string.Equals(file, targetPath, StringComparison.OrdinalIgnoreCase) };
            Thumbnails.Add(item);
            if (item.IsSelected) SelectedThumbnail = item;
        }

        OnPropertyChanged(nameof(ThumbnailPageCount));
        OnPropertyChanged(nameof(ThumbnailPageLabel));
        RaiseAllCommandsCanExecuteChanged();

        // Decode thumbnail images for this page in the background so it doesn't stall the UI.
        foreach (var item in Thumbnails.ToList())
        {
            var thumbResult = await _loaderService.LoadThumbnailAsync(item.FilePath, 96);
            if (thumbResult.Success)
                item.Thumbnail = PixelBufferBitmapConverter.ToBitmapSource(thumbResult.Value!);
        }
    }

    /// <summary>Saves back to the file the document was opened from, in its original format, without prompting.</summary>
    private async Task SaveAsync()
    {
        if (Document is null) return;
        await SaveToAsync(Document.SourceFilePath, Document.OriginalFormat, confirmOverwrite: false, promptForQuality: false);
    }

    private async Task SaveAsAsync()
    {
        if (Document is null) return;

        var suggestedName = Path.GetFileNameWithoutExtension(Document.SourceFilePath);
        var choice = _dialogService.ShowSaveAsDialog(suggestedName, Document.OriginalFormat);
        if (choice is null) return;

        var (outputPath, format) = choice.Value;
        await SaveToAsync(outputPath, format, confirmOverwrite: true, promptForQuality: true);
    }

    private async Task SaveToAsync(string outputPath, ImageFormatType format, bool confirmOverwrite, bool promptForQuality)
    {
        if (Document is null) return;

        var overwritesOriginal = string.Equals(
            Path.GetFullPath(outputPath), Path.GetFullPath(Document.SourceFilePath), StringComparison.OrdinalIgnoreCase);

        if (overwritesOriginal && confirmOverwrite)
        {
            var confirmed = _dialogService.ShowConfirm(
                "Overwrite Original?",
                "This will overwrite the original file you opened. This cannot be undone. Continue?");
            if (!confirmed) return;
        }

        var quality = _lastUsedQuality;
        if (format is ImageFormatType.Jpeg or ImageFormatType.WebP && promptForQuality)
        {
            var chosenQuality = _dialogService.ShowQualityDialog(_lastUsedQuality);
            if (chosenQuality is null) return; // cancelled
            quality = chosenQuality.Value;
            _lastUsedQuality = quality;
        }

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

    private async Task RotateAsync(RotateDirection direction)
    {
        if (Document is null) return;

        await RunEditAsync("Rotating...", async () =>
        {
            var result = await _transformService.RotateAsync(Document.PixelBuffer, direction);
            if (!result.Success)
            {
                _dialogService.ShowError("Rotate Failed", result.ErrorMessage!);
                return false;
            }

            Document.PixelBuffer = result.Value!;
            return true;
        }, checkBackgroundIntegrity: false); // rotation moves the corners by design
    }

    // --- Eyedropper ----------------------------------------------------

    /// <summary>
    /// Samples the pixel at the given image-pixel coordinate and stores it as
    /// <see cref="PickedColorArgb"/>. Single-shot, like the classic Paint
    /// eyedropper: picking a color deactivates the tool immediately.
    /// </summary>
    public void PickColorAt(int x, int y)
    {
        if (Document is null) return;

        var buffer = Document.PixelBuffer;
        if (x < 0 || y < 0 || x >= buffer.Width || y >= buffer.Height) return;

        var offset = y * buffer.Stride + x * 4;
        byte b = buffer.Pixels[offset];
        byte g = buffer.Pixels[offset + 1];
        byte r = buffer.Pixels[offset + 2];
        byte a = buffer.Pixels[offset + 3];

        PickedColorArgb = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        CurrentTool = ToolMode.None;
        StatusText = $"Picked color {PickedColorHex}.";
    }

    // --- Adjustments and filters -----------------------------------------

    private async Task AdjustAsync()
    {
        if (Document is null) return;

        var request = _dialogService.ShowAdjustmentsDialog();
        if (request is null || request.IsNoOp) return;

        await RunEditAsync("Adjusting...", async () =>
        {
            var result = await _adjustmentService.ApplyAdjustmentsAsync(Document.PixelBuffer, request);
            if (!result.Success)
            {
                _dialogService.ShowError("Adjustment Failed", result.ErrorMessage!);
                return false;
            }

            Document.PixelBuffer = result.Value!;
            return true;
        });
    }

    private async Task ApplyFilterAsync(ImageFilterType filterType)
    {
        if (Document is null) return;

        await RunEditAsync($"Applying {filterType}...", async () =>
        {
            var result = await _filterService.ApplyFilterAsync(Document.PixelBuffer, filterType);
            if (!result.Success)
            {
                _dialogService.ShowError("Filter Failed", result.ErrorMessage!);
                return false;
            }

            Document.PixelBuffer = result.Value!;
            return true;
        });
    }

    /// <summary>
    /// AI background removal: predicts a foreground mask and makes the
    /// background transparent. Run "Change Background..." afterwards to fill
    /// or replace it, reusing the existing chroma-key-mode pipeline.
    /// </summary>
    private async Task AiRemoveBackgroundAsync()
    {
        if (Document is null) return;

        await RunEditAsync("Removing background (AI)...", async () =>
        {
            var result = await _segmentationService.RemoveBackgroundAsync(Document.PixelBuffer);
            if (!result.Success)
            {
                _dialogService.ShowError("AI Background Removal Failed", result.ErrorMessage!);
                return false;
            }

            Document.PixelBuffer = result.Value!;
            return true;
        }, checkBackgroundIntegrity: false); // removing the background is the whole point of this operation
    }

    // --- Navigation --------------------------------------------------

    private bool CanNavigate(int offset)
    {
        if (Document is null || _folderFiles.Count == 0) return false;
        var index = _folderFiles.FindIndex(f => string.Equals(f, Document.SourceFilePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        var target = index + offset;
        return target >= 0 && target < _folderFiles.Count;
    }

    private async Task NavigateAsync(int offset)
    {
        if (Document is null) return;

        var index = _folderFiles.FindIndex(f => string.Equals(f, Document.SourceFilePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;

        var target = index + offset;
        if (target < 0 || target >= _folderFiles.Count) return;

        await LoadFileAsync(_folderFiles[target]);
    }

    // --- Interactive crop ----------------------------------------------

    /// <summary>Thin passthrough so the window's code-behind (which owns the crop-selection UI) can prompt without touching <see cref="IDialogService"/> directly.</summary>
    public CropMode? PromptCropMode() => _dialogService.ShowCropConfirmDialog();

    /// <summary>
    /// Applies a confirmed interactive crop selection. <see cref="CropMode.ReplaceExisting"/>
    /// goes through the normal undo-able edit pipeline; <see cref="CropMode.ExtractAsNew"/>
    /// exports just the selected region to a new file and leaves the open document untouched.
    /// </summary>
    public async Task ApplyInteractiveCropAsync(int x, int y, int width, int height, CropMode mode)
    {
        if (Document is null) return;

        var request = new CropRequest { X = x, Y = y, Width = width, Height = height };

        if (mode == CropMode.ReplaceExisting)
        {
            await RunEditAsync("Cropping...", async () =>
            {
                var result = await _cropService.CropAsync(Document.PixelBuffer, request);
                if (!result.Success)
                {
                    _dialogService.ShowError("Crop Failed", result.ErrorMessage!);
                    return false;
                }

                Document.PixelBuffer = result.Value!;
                return true;
            });
            return;
        }

        // ExtractAsNew: crop, then Save-As the result. The open document (and
        // undo stack) is never touched, and the original file on disk is
        // never overwritten implicitly - the export path enforces that.
        IsBusy = true;
        StatusText = "Cropping...";
        try
        {
            var cropResult = await _cropService.CropAsync(Document.PixelBuffer, request);
            if (!cropResult.Success)
            {
                _dialogService.ShowError("Crop Failed", cropResult.ErrorMessage!);
                return;
            }

            var suggestedName = Path.GetFileNameWithoutExtension(Document.SourceFilePath) + "_crop";
            var choice = _dialogService.ShowSaveAsDialog(suggestedName, Document.OriginalFormat);
            if (choice is null) return;

            var (outputPath, format) = choice.Value;
            var exportRequest = new ExportRequest
            {
                OutputFilePath = outputPath,
                TargetFormat = format,
                JpegQuality = _lastUsedQuality,
                PreserveMetadata = true,
                AllowOverwriteOriginal = false,
                BackgroundFillArgb = PickedColorArgb
            };

            var saveResult = await _exportService.SaveAsAsync(cropResult.Value!, Document.Metadata, Document.SourceFilePath, exportRequest);
            if (!saveResult.Success)
            {
                _dialogService.ShowError("Couldn't Save Cropped Image", saveResult.ErrorMessage!);
                return;
            }

            StatusText = $"Cropped region saved to {saveResult.Value}. Original image unchanged.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EditTextLayersAsync()
    {
        if (Document is null) return;

        var layers = _dialogService.ShowTextOverlayDialog(Document.TextLayers, Document.PixelBuffer.Width, Document.PixelBuffer.Height, PickedColorArgb);
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

        var request = _dialogService.ShowBackgroundDialog(PickedColorArgb);
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
        }, checkBackgroundIntegrity: false); // changing the background is the whole point of this operation
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
    /// <param name="checkBackgroundIntegrity">
    /// Whether to compare corner-sampled background color before/after and
    /// log a warning if it shifted unexpectedly. Pass false for operations
    /// where a background/corner color change is the intended outcome (e.g.
    /// rotate, which moves corners; background replacement, whose entire
    /// point is changing the background).
    /// </param>
    private async Task RunEditAsync(string busyText, Func<Task<bool>> editAction, bool checkBackgroundIntegrity = true)
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

            if (checkBackgroundIntegrity)
            {
                var integrity = _integrityService.CheckBackgroundColor(preEditSnapshot.PixelBuffer, Document.PixelBuffer);
                if (!integrity.IsWithinTolerance)
                {
                    foreach (var warning in integrity.Warnings)
                        _logger.LogWarning(warning);
                }
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
        if (Document is null) { CurrentImageSource = null; return; }

        var buffer = IsShowingOriginal ? Document.OriginalPixelBuffer : Document.PixelBuffer;
        CurrentImageSource = PixelBufferBitmapConverter.ToBitmapSource(buffer);
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
