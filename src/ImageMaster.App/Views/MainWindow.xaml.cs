using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private TextOverlayLayer? _draggingLayer;
    private Point _dragStartMouse;
    private Point _dragStartLayerPosition;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Document) or nameof(MainViewModel.CurrentImageSource))
            RefreshTextOverlay();
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

    // --- Toolbar-adjacent UI actions not expressed as commands ------------

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
