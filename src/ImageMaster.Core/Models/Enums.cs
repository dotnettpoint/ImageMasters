namespace ImageMaster.Core.Models;

/// <summary>
/// Image file formats supported for opening and saving.
/// </summary>
public enum ImageFormatType
{
    Jpeg,
    Png,
    Bmp,
    Gif,
    Tiff,
    WebP
}

/// <summary>
/// How the pixel buffer's raw bytes are laid out. Everything in this app is
/// normalized to <see cref="Bgra8888"/> (4 bytes per pixel, pre-multiplied
/// alpha) so that Core/Infrastructure never have to branch on pixel format,
/// and so the buffer maps directly onto a WPF <c>WriteableBitmap</c> using
/// <c>PixelFormats.Pbgra32</c>.
/// </summary>
public enum PixelFormatKind
{
    Bgra8888
}

/// <summary>
/// How a background replacement should be performed.
/// </summary>
public enum BackgroundReplaceMode
{
    /// <summary>Fill transparent pixels (alpha &lt; 255) with a solid color.</summary>
    SolidColorForTransparent,

    /// <summary>Composite transparent pixels over a replacement image.</summary>
    ImageForTransparent,

    /// <summary>
    /// Image has no usable transparency: remove pixels close to a chosen key
    /// color (simple color-distance threshold) and replace them with a solid
    /// color. This is a basic chroma-key approach, not real segmentation.
    /// </summary>
    ChromaKeyToSolidColor,

    /// <summary>
    /// Same threshold-based key removal as <see cref="ChromaKeyToSolidColor"/>,
    /// but composites the result over a replacement image instead.
    /// </summary>
    ChromaKeyToImage
}
