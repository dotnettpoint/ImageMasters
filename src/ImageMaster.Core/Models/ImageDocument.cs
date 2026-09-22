namespace ImageMaster.Core.Models;

/// <summary>
/// A single open document: the pixel data being edited, its original file
/// info, metadata, and any text layers not yet flattened onto the pixels.
/// </summary>
public sealed class ImageDocument
{
    public string SourceFilePath { get; init; } = string.Empty;
    public ImageFormatType OriginalFormat { get; init; }
    public ImagePixelBuffer PixelBuffer { get; set; }
    public ImageMetadata Metadata { get; set; }

    /// <summary>Unflattened text overlays, kept editable until the user flattens or saves.</summary>
    public List<TextOverlayLayer> TextLayers { get; } = new();

    public bool HasUnsavedChanges { get; set; }

    public ImageDocument(string sourceFilePath, ImageFormatType originalFormat, ImagePixelBuffer pixelBuffer, ImageMetadata metadata)
    {
        SourceFilePath = sourceFilePath;
        OriginalFormat = originalFormat;
        PixelBuffer = pixelBuffer;
        Metadata = metadata;
    }
}
