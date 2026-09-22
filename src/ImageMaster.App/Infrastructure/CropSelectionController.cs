using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ImageMaster.App.Infrastructure;

/// <summary>
/// Drives an interactive, Paint.NET-style rectangular crop selection drawn
/// directly on an overlay <see cref="Canvas"/> that sits over the image at
/// 1:1 pixel scale (so every coordinate here is an image-pixel coordinate;
/// zoom is handled transparently by the canvas's own layout transform, the
/// same trick the text-overlay canvas uses). Owns and manages its own
/// visuals (dimming bands, selection border, resize handles, live dimension
/// label) so the host window only needs to activate/deactivate it and react
/// to its events.
/// </summary>
public sealed class CropSelectionController
{
    private const double HandleSize = 10;
    private const double MinSelectionSize = 4;

    private enum HandleKind { TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }
    private enum DragMode { None, Creating, Moving }

    private readonly Canvas _canvas;
    private readonly Rectangle _dimTop, _dimBottom, _dimLeft, _dimRight;
    private readonly Rectangle _selectionBorder;
    private readonly Border _dimensionLabel;
    private readonly TextBlock _dimensionText;
    private readonly Dictionary<HandleKind, Thumb> _handles = new();

    private int _imageWidth, _imageHeight;
    private double? _aspectRatio; // width / height; null = free

    private double _x, _y, _width, _height; // current selection, image-pixel space
    private bool _hasSelection;

    private DragMode _dragMode = DragMode.None;
    private Point _dragStart;
    private double _dragStartX, _dragStartY;

    /// <summary>Fires whenever the selection rectangle changes shape or position.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Fires when the user confirms the current selection (double-click inside it, or <see cref="ConfirmIfValid"/>).</summary>
    public event EventHandler? SelectionConfirmed;

    /// <summary>Fires when <see cref="Cancel"/> is called (e.g. the host mapped the Esc key to it).</summary>
    public event EventHandler? Cancelled;

    public CropSelectionController(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.PreviewMouseLeftButtonDown += OnCanvasMouseDown;
        _canvas.PreviewMouseMove += OnCanvasMouseMove;
        _canvas.PreviewMouseLeftButtonUp += OnCanvasMouseUp;

        _dimTop = CreateDimBand();
        _dimBottom = CreateDimBand();
        _dimLeft = CreateDimBand();
        _dimRight = CreateDimBand();

        _selectionBorder = new Rectangle
        {
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        _dimensionText = new TextBlock { Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(4, 1, 4, 1) };
        _dimensionLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            Child = _dimensionText,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        _canvas.Children.Add(_dimTop);
        _canvas.Children.Add(_dimBottom);
        _canvas.Children.Add(_dimLeft);
        _canvas.Children.Add(_dimRight);
        _canvas.Children.Add(_selectionBorder);
        _canvas.Children.Add(_dimensionLabel);

        foreach (var kind in Enum.GetValues<HandleKind>())
        {
            var handle = CreateHandle(kind);
            _handles[kind] = handle;
            _canvas.Children.Add(handle);
        }
    }

    private static Rectangle CreateDimBand() => new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };

    private Thumb CreateHandle(HandleKind kind)
    {
        var thumb = new Thumb
        {
            Width = HandleSize,
            Height = HandleSize,
            Background = Brushes.White,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Cursor = CursorForHandle(kind),
            Visibility = Visibility.Collapsed
        };
        thumb.DragDelta += (_, e) => OnHandleDragDelta(kind, e);
        return thumb;
    }

    private static Cursor CursorForHandle(HandleKind kind) => kind switch
    {
        HandleKind.TopLeft or HandleKind.BottomRight => Cursors.SizeNWSE,
        HandleKind.TopRight or HandleKind.BottomLeft => Cursors.SizeNESW,
        HandleKind.Top or HandleKind.Bottom => Cursors.SizeNS,
        _ => Cursors.SizeWE
    };

    // --- Lifecycle ------------------------------------------------------

    public void Activate(int imageWidth, int imageHeight)
    {
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;
        _canvas.Visibility = Visibility.Visible;
        _hasSelection = false;
        RenderVisuals();
    }

    public void Deactivate()
    {
        _hasSelection = false;
        _dragMode = DragMode.None;
        _canvas.Visibility = Visibility.Collapsed;
    }

    /// <summary>Sets the locked aspect ratio (width/height), or null for a free-form selection. Re-fits any current selection to it.</summary>
    public void SetAspectRatio(double? ratio)
    {
        _aspectRatio = ratio;
        if (_hasSelection && ratio.HasValue)
        {
            _height = _width / ratio.Value;
            ClampToBounds();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        RenderVisuals();
    }

    public bool HasValidSelection => _hasSelection && _width >= MinSelectionSize && _height >= MinSelectionSize;

    public (int X, int Y, int Width, int Height)? GetSelectionRect() =>
        HasValidSelection ? ((int)Math.Round(_x), (int)Math.Round(_y), (int)Math.Round(_width), (int)Math.Round(_height)) : null;

    public void ConfirmIfValid()
    {
        if (HasValidSelection) SelectionConfirmed?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    public void ResetSelection()
    {
        _hasSelection = false;
        RenderVisuals();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // --- Mouse-driven create/move ----------------------------------------

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb) return; // handles manage their own drag via DragDelta

        var point = e.GetPosition(_canvas);

        if (e.ClickCount == 2 && HasValidSelection && IsInsideSelection(point))
        {
            ConfirmIfValid();
            e.Handled = true;
            return;
        }

        if (HasValidSelection && IsInsideSelection(point))
        {
            _dragMode = DragMode.Moving;
            _dragStart = point;
            _dragStartX = _x;
            _dragStartY = _y;
        }
        else
        {
            _dragMode = DragMode.Creating;
            _dragStart = ClampPoint(point);
            _x = _dragStart.X;
            _y = _dragStart.Y;
            _width = 0;
            _height = 0;
            _hasSelection = true;
        }

        _canvas.CaptureMouse();
        RenderVisuals();
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.None) return;

        var point = ClampPoint(e.GetPosition(_canvas));

        if (_dragMode == DragMode.Creating)
        {
            var x0 = Math.Min(_dragStart.X, point.X);
            var y0 = Math.Min(_dragStart.Y, point.Y);
            var w = Math.Abs(point.X - _dragStart.X);
            var h = Math.Abs(point.Y - _dragStart.Y);

            if (_aspectRatio is double ratio)
            {
                h = w / ratio;
                if (point.X < _dragStart.X) x0 = _dragStart.X - w;
                if (point.Y < _dragStart.Y) y0 = _dragStart.Y - h;
            }

            _x = x0; _y = y0; _width = w; _height = h;
        }
        else // Moving
        {
            _x = _dragStartX + (point.X - _dragStart.X);
            _y = _dragStartY + (point.Y - _dragStart.Y);
        }

        ClampToBounds();
        RenderVisuals();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None) return;
        _dragMode = DragMode.None;
        _canvas.ReleaseMouseCapture();
    }

    private bool IsInsideSelection(Point point) =>
        point.X >= _x && point.X <= _x + _width && point.Y >= _y && point.Y <= _y + _height;

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, _imageWidth),
        Math.Clamp(point.Y, 0, _imageHeight));

    // --- Handle-driven resize --------------------------------------------

    private void OnHandleDragDelta(HandleKind kind, DragDeltaEventArgs e)
    {
        if (!_hasSelection) return;

        var left = _x; var top = _y; var right = _x + _width; var bottom = _y + _height;

        switch (kind)
        {
            case HandleKind.TopLeft: left += e.HorizontalChange; top += e.VerticalChange; break;
            case HandleKind.Top: top += e.VerticalChange; break;
            case HandleKind.TopRight: right += e.HorizontalChange; top += e.VerticalChange; break;
            case HandleKind.Right: right += e.HorizontalChange; break;
            case HandleKind.BottomRight: right += e.HorizontalChange; bottom += e.VerticalChange; break;
            case HandleKind.Bottom: bottom += e.VerticalChange; break;
            case HandleKind.BottomLeft: left += e.HorizontalChange; bottom += e.VerticalChange; break;
            case HandleKind.Left: left += e.HorizontalChange; break;
        }

        left = Math.Clamp(left, 0, right - MinSelectionSize);
        top = Math.Clamp(top, 0, bottom - MinSelectionSize);
        right = Math.Clamp(right, left + MinSelectionSize, _imageWidth);
        bottom = Math.Clamp(bottom, top + MinSelectionSize, _imageHeight);

        _x = left; _y = top; _width = right - left; _height = bottom - top;

        if (_aspectRatio is double ratio)
        {
            // Locked-aspect resizing is corner-only (edge handles are hidden
            // in that mode) - re-derive height from width so the ratio stays exact.
            _height = _width / ratio;
            if (top + _height > _imageHeight) _height = _imageHeight - top;
            if (kind is HandleKind.TopLeft or HandleKind.TopRight) _y = bottom - _height;
        }

        RenderVisuals();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // --- Visual layout ----------------------------------------------------

    private void ClampToBounds()
    {
        _width = Math.Max(0, Math.Min(_width, _imageWidth));
        _height = Math.Max(0, Math.Min(_height, _imageHeight));
        _x = Math.Clamp(_x, 0, _imageWidth - _width);
        _y = Math.Clamp(_y, 0, _imageHeight - _height);
    }

    private void UpdateHandleVisibility()
    {
        var showAll = _aspectRatio is null;
        foreach (var (kind, thumb) in _handles)
        {
            var isCorner = kind is HandleKind.TopLeft or HandleKind.TopRight or HandleKind.BottomLeft or HandleKind.BottomRight;
            thumb.Visibility = _hasSelection && (showAll || isCorner) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RenderVisuals()
    {
        if (!_hasSelection)
        {
            _selectionBorder.Visibility = Visibility.Collapsed;
            _dimensionLabel.Visibility = Visibility.Collapsed;
            _dimTop.Visibility = _dimBottom.Visibility = _dimLeft.Visibility = _dimRight.Visibility = Visibility.Collapsed;
            foreach (var thumb in _handles.Values) thumb.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(_selectionBorder, _x);
        Canvas.SetTop(_selectionBorder, _y);
        _selectionBorder.Width = _width;
        _selectionBorder.Height = _height;
        _selectionBorder.Visibility = Visibility.Visible;

        // Four bands tile the whole canvas minus the selection rectangle:
        // a full-width top strip, a full-width bottom strip, and left/right
        // strips confined to the selection's own vertical span.
        Canvas.SetLeft(_dimTop, 0); Canvas.SetTop(_dimTop, 0);
        _dimTop.Width = _imageWidth; _dimTop.Height = _y;

        Canvas.SetLeft(_dimBottom, 0); Canvas.SetTop(_dimBottom, _y + _height);
        _dimBottom.Width = _imageWidth; _dimBottom.Height = Math.Max(0, _imageHeight - (_y + _height));

        Canvas.SetLeft(_dimLeft, 0); Canvas.SetTop(_dimLeft, _y);
        _dimLeft.Width = _x; _dimLeft.Height = _height;

        Canvas.SetLeft(_dimRight, _x + _width); Canvas.SetTop(_dimRight, _y);
        _dimRight.Width = Math.Max(0, _imageWidth - (_x + _width)); _dimRight.Height = _height;

        _dimTop.Visibility = _dimBottom.Visibility = _dimLeft.Visibility = _dimRight.Visibility = Visibility.Visible;

        foreach (var (kind, thumb) in _handles)
        {
            var (hx, hy) = HandleCenter(kind);
            Canvas.SetLeft(thumb, hx - HandleSize / 2);
            Canvas.SetTop(thumb, hy - HandleSize / 2);
        }
        UpdateHandleVisibility();

        _dimensionText.Text = $"{(int)Math.Round(_width)} x {(int)Math.Round(_height)} px";
        Canvas.SetLeft(_dimensionLabel, _x);
        Canvas.SetTop(_dimensionLabel, Math.Max(0, _y - 20));
        _dimensionLabel.Visibility = Visibility.Visible;
    }

    private (double X, double Y) HandleCenter(HandleKind kind) => kind switch
    {
        HandleKind.TopLeft => (_x, _y),
        HandleKind.Top => (_x + _width / 2, _y),
        HandleKind.TopRight => (_x + _width, _y),
        HandleKind.Right => (_x + _width, _y + _height / 2),
        HandleKind.BottomRight => (_x + _width, _y + _height),
        HandleKind.Bottom => (_x + _width / 2, _y + _height),
        HandleKind.BottomLeft => (_x, _y + _height),
        HandleKind.Left => (_x, _y + _height / 2),
        _ => (_x, _y)
    };
}
