using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMaster.App.Infrastructure;
using ImageMaster.App.ViewModels;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>
/// Code-behind is intentionally limited to what MVVM data binding can't
/// express cleanly: drag-to-position text overlays on the canvas (which
/// needs raw mouse coordinates) and computing a "fit to window" zoom level
/// from the ScrollViewer's actual size. Every actual edit still goes through
/// <see cref="MainViewModel"/> commands.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly CropSelectionController _cropController;
    private TextOverlayLayer? _draggingLayer;
    private Point _dragStartMouse;
    private Point _dragStartLayerPosition;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _cropController = new CropSelectionController(CropOverlayCanvas);
        _cropController.SelectionChanged += (_, _) => ApplyCropButton.IsEnabled = _cropController.HasValidSelection;
        _cropController.SelectionConfirmed += async (_, _) => await ConfirmCropAsync();
        _cropController.Cancelled += (_, _) => _viewModel.CurrentTool = ToolMode.None;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Document) or nameof(MainViewModel.CurrentImageSource))
            RefreshTextOverlay();

        if (e.PropertyName is nameof(MainViewModel.CurrentTool))
        {
            UpdateCursorForCurrentTool();
            UpdateCropToolState();
        }
    }

    private void UpdateCursorForCurrentTool()
    {
        ImageHost.Cursor = _viewModel.CurrentTool switch
        {
            ToolMode.Eyedropper => Cursors.Cross,
            ToolMode.Crop => Cursors.Cross,
            _ => Cursors.Arrow
        };

        if (_viewModel.CurrentTool != ToolMode.Eyedropper)
            MagnifierLoupe.Visibility = Visibility.Collapsed;
    }

    // --- Crop tool ------------------------------------------------------

    private void UpdateCropToolState()
    {
        if (_viewModel.CurrentTool == ToolMode.Crop)
        {
            var source = _viewModel.CurrentImageSource;
            if (source is null)
            {
                _viewModel.CurrentTool = ToolMode.None;
                return;
            }

            _cropController.Activate(source.PixelWidth, source.PixelHeight);
            CropOptionsBar.Visibility = Visibility.Visible;
            ApplyCropButton.IsEnabled = false;
        }
        else
        {
            _cropController.Deactivate();
            CropOptionsBar.Visibility = Visibility.Collapsed;
        }
    }

    private async Task ConfirmCropAsync()
    {
        var rect = _cropController.GetSelectionRect();
        if (rect is null) return;

        var mode = _viewModel.PromptCropMode();
        if (mode is null) return; // cancelled - stay in crop tool with the selection intact

        var (x, y, width, height) = rect.Value;
        _viewModel.CurrentTool = ToolMode.None;
        await _viewModel.ApplyInteractiveCropAsync(x, y, width, height, mode.Value);
    }

    private void OnAspectRatioChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cropController is null) return; // XAML selects the first ComboBoxItem during InitializeComponent, before the constructor body runs

        var label = (AspectRatioCombo.SelectedItem as ComboBoxItem)?.Content as string;
        double? ratio = label switch
        {
            "1:1" => 1.0,
            "4:3" => 4.0 / 3.0,
            "16:9" => 16.0 / 9.0,
            _ => null
        };
        _cropController.SetAspectRatio(ratio);
    }

    private async void OnApplyCropClick(object sender, RoutedEventArgs e) => await ConfirmCropAsync();

    private void OnCancelCropClick(object sender, RoutedEventArgs e) => _cropController.Cancel();

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.CurrentTool != ToolMode.Crop) return;

        if (e.Key == Key.Enter) { _cropController.ConfirmIfValid(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _cropController.Cancel(); e.Handled = true; }
    }

    /// <summary>Rebuilds the overlay Canvas's TextBlocks from the document's current (unflattened) text layers.</summary>
    private void RefreshTextOverlay()
    {
        TextOverlayCanvas.Children.Clear();

        var layers = _viewModel.Document?.TextLayers;
        if (layers is null) return;

        foreach (var layer in layers)
        {
            var block = BuildOverlayTextBlock(layer);
            Canvas.SetLeft(block, layer.X);
            Canvas.SetTop(block, layer.Y);
            TextOverlayCanvas.Children.Add(block);
        }
    }

    private static TextBlock BuildOverlayTextBlock(TextOverlayLayer layer)
    {
        var argb = layer.ArgbColor;
        var color = Color.FromArgb((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

        return new TextBlock
        {
            Text = layer.Text,
            FontFamily = new FontFamily(layer.FontFamily),
            FontSize = layer.FontSizePoints * 96.0 / 72.0, // points -> device-independent pixels
            FontWeight = layer.IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = layer.IsItalic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = new SolidColorBrush(color),
            Opacity = layer.OpacityPercent / 100.0,
            Tag = layer,
            Cursor = Cursors.SizeAll
        };
    }

    // --- Drag-to-position for text overlays -------------------------------

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not TextBlock { Tag: TextOverlayLayer layer } block) return;

        _draggingLayer = layer;
        _dragStartMouse = e.GetPosition(TextOverlayCanvas);
        _dragStartLayerPosition = new Point(layer.X, layer.Y);
        block.CaptureMouse();
        e.Handled = true;
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingLayer is null || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(TextOverlayCanvas);
        var offsetX = current.X - _dragStartMouse.X;
        var offsetY = current.Y - _dragStartMouse.Y;

        _draggingLayer.X = _dragStartLayerPosition.X + offsetX;
        _draggingLayer.Y = _dragStartLayerPosition.Y + offsetY;

        var block = TextOverlayCanvas.Children
            .OfType<TextBlock>()
            .FirstOrDefault(b => ReferenceEquals(b.Tag, _draggingLayer));
        if (block is not null)
        {
            Canvas.SetLeft(block, _draggingLayer.X);
            Canvas.SetTop(block, _draggingLayer.Y);
        }
    }

    private void OnOverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingLayer is null) return;

        if (_viewModel.Document is not null)
            _viewModel.Document.HasUnsavedChanges = true;

        _draggingLayer = null;
        Mouse.Capture(null);
    }

    // --- Eyedropper ---------------------------------------------------

    private void OnImageHostMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.CurrentTool != ToolMode.Eyedropper) return;
        if (_viewModel.CurrentImageSource is null) return;

        var position = e.GetPosition(MainImage);
        var x = (int)Math.Floor(position.X);
        var y = (int)Math.Floor(position.Y);

        _viewModel.PickColorAt(x, y);
        MagnifierLoupe.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnImageHostMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel.CurrentTool != ToolMode.Eyedropper) return;

        var source = _viewModel.CurrentImageSource;
        if (source is null)
        {
            MagnifierLoupe.Visibility = Visibility.Collapsed;
            return;
        }

        var imagePos = e.GetPosition(MainImage);
        var x = (int)Math.Floor(imagePos.X);
        var y = (int)Math.Floor(imagePos.Y);
        if (x < 0 || y < 0 || x >= source.PixelWidth || y >= source.PixelHeight)
        {
            MagnifierLoupe.Visibility = Visibility.Collapsed;
            return;
        }

        const int sampleRadius = 8;
        var left = Math.Clamp(x - sampleRadius, 0, source.PixelWidth - 1);
        var top = Math.Clamp(y - sampleRadius, 0, source.PixelHeight - 1);
        var size = Math.Min(sampleRadius * 2, Math.Min(source.PixelWidth - left, source.PixelHeight - top));
        if (size <= 0)
        {
            MagnifierLoupe.Visibility = Visibility.Collapsed;
            return;
        }

        MagnifierImage.Source = new CroppedBitmap(source, new Int32Rect(left, top, size, size));

        // Position near the cursor in Window coordinates, flipping to the
        // opposite side when close to an edge so the loupe stays fully visible.
        var windowPos = e.GetPosition(this);
        const double offset = 24, loupeSize = 130;
        var loupeLeft = windowPos.X + offset + loupeSize > ActualWidth ? windowPos.X - offset - loupeSize : windowPos.X + offset;
        var loupeTop = windowPos.Y + offset + loupeSize > ActualHeight ? windowPos.Y - offset - loupeSize : windowPos.Y + offset;
        Canvas.SetLeft(MagnifierLoupe, loupeLeft);
        Canvas.SetTop(MagnifierLoupe, loupeTop);
        MagnifierLoupe.Visibility = Visibility.Visible;
    }

    private void OnImageHostMouseLeave(object sender, MouseEventArgs e)
    {
        MagnifierLoupe.Visibility = Visibility.Collapsed;
    }

    // --- Toolbar-adjacent UI actions not expressed as commands ------------

    /// <summary>
    /// Ctrl+Scroll zooms in/out centered on the cursor (plain scroll keeps its
    /// normal ScrollViewer vertical-pan behavior) - matches Paint.NET's
    /// scroll-to-zoom convention.
    /// </summary>
    private void OnImageScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (_viewModel.CurrentImageSource is null) return;

        e.Handled = true;

        var mouseAtViewport = e.GetPosition(ImageScrollViewer);
        var oldScale = _viewModel.ZoomPercentage / 100.0;
        var contentPoint = new Point(
            (ImageScrollViewer.HorizontalOffset + mouseAtViewport.X) / oldScale,
            (ImageScrollViewer.VerticalOffset + mouseAtViewport.Y) / oldScale);

        var step = e.Delta > 0 ? 10 : -10;
        _viewModel.ZoomPercentage += step;

        // The ScaleTransform only takes effect after a layout pass, so defer
        // re-centering the viewport until then.
        Dispatcher.InvokeAsync(() =>
        {
            var newScale = _viewModel.ZoomPercentage / 100.0;
            ImageScrollViewer.ScrollToHorizontalOffset(contentPoint.X * newScale - mouseAtViewport.X);
            ImageScrollViewer.ScrollToVerticalOffset(contentPoint.Y * newScale - mouseAtViewport.Y);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnFitToWindowClick(object sender, RoutedEventArgs e)
    {
        var source = _viewModel.CurrentImageSource;
        if (source is null) return;

        var availableWidth = ImageScrollViewer.ActualWidth - SystemParameters.VerticalScrollBarWidth;
        var availableHeight = ImageScrollViewer.ActualHeight - SystemParameters.HorizontalScrollBarHeight;
        if (availableWidth <= 0 || availableHeight <= 0) return;

        var scale = Math.Min(availableWidth / source.PixelWidth, availableHeight / source.PixelHeight);
        _viewModel.ZoomPercentage = scale * 100.0;
    }

    private void OnThumbnailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedThumbnail is not null && _viewModel.SelectThumbnailCommand.CanExecute(null))
            _viewModel.SelectThumbnailCommand.Execute(null);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
