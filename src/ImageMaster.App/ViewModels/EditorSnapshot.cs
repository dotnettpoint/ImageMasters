using ImageMaster.Core.Models;

namespace ImageMaster.App.ViewModels;

/// <summary>
/// Immutable-by-convention snapshot of everything undo/redo needs to restore:
/// the pixel buffer plus the (unflattened) text layers at that point.
/// </summary>
public sealed class EditorSnapshot
{
    public ImagePixelBuffer PixelBuffer { get; }
    public IReadOnlyList<TextOverlayLayer> TextLayers { get; }

    public EditorSnapshot(ImagePixelBuffer pixelBuffer, IReadOnlyList<TextOverlayLayer> textLayers)
    {
        PixelBuffer = pixelBuffer;
        TextLayers = textLayers;
    }

    public static EditorSnapshot CaptureFrom(ImageDocument document) => new(
        document.PixelBuffer.Clone(),
        document.TextLayers.Select(CloneLayer).ToList());

    private static TextOverlayLayer CloneLayer(TextOverlayLayer source) => new()
    {
        Id = source.Id,
        Text = source.Text,
        FontFamily = source.FontFamily,
        FontSizePoints = source.FontSizePoints,
        IsBold = source.IsBold,
        IsItalic = source.IsItalic,
        OpacityPercent = source.OpacityPercent,
        ArgbColor = source.ArgbColor,
        X = source.X,
        Y = source.Y
    };
}
