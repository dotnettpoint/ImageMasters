using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;

namespace ImageMaster.Infrastructure.Services;

/// <summary>Applies one-click stylistic filters (Grayscale, Sepia, Invert) via per-pixel color math.</summary>
public sealed class SkiaImageFilterService : IImageFilterService
{
    private readonly IAppLogger _logger;

    public SkiaImageFilterService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<OperationResult<ImagePixelBuffer>> ApplyFilterAsync(ImagePixelBuffer source, ImageFilterType filterType, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var pixels = new byte[source.Pixels.Length];
                Buffer.BlockCopy(source.Pixels, 0, pixels, 0, source.Pixels.Length);

                for (var y = 0; y < source.Height; y++)
                {
                    var rowOffset = y * source.Stride;
                    for (var x = 0; x < source.Width; x++)
                    {
                        var offset = rowOffset + x * 4;
                        var b = pixels[offset];
                        var g = pixels[offset + 1];
                        var r = pixels[offset + 2];

                        var (nr, ng, nb) = filterType switch
                        {
                            ImageFilterType.Grayscale => ToGrayscale(r, g, b),
                            ImageFilterType.Sepia => ToSepia(r, g, b),
                            ImageFilterType.Invert => ((byte)(255 - r), (byte)(255 - g), (byte)(255 - b)),
                            _ => (r, g, b)
                        };

                        pixels[offset] = nb;
                        pixels[offset + 1] = ng;
                        pixels[offset + 2] = nr;
                    }

                    if (y % 64 == 0) cancellationToken.ThrowIfCancellationRequested();
                }

                cancellationToken.ThrowIfCancellationRequested();
                return OperationResult<ImagePixelBuffer>.Ok(new ImagePixelBuffer(source.Width, source.Height, source.Stride, source.Format, pixels));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Filter operation failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("The filter couldn't be applied. The image may be corrupt or too large.");
            }
        }, cancellationToken);
    }

    private static (byte R, byte G, byte B) ToGrayscale(byte r, byte g, byte b)
    {
        var l = (byte)Math.Clamp(0.299 * r + 0.587 * g + 0.114 * b, 0, 255);
        return (l, l, l);
    }

    private static (byte R, byte G, byte B) ToSepia(byte r, byte g, byte b)
    {
        var nr = (byte)Math.Clamp(0.393 * r + 0.769 * g + 0.189 * b, 0, 255);
        var ng = (byte)Math.Clamp(0.349 * r + 0.686 * g + 0.168 * b, 0, 255);
        var nb = (byte)Math.Clamp(0.272 * r + 0.534 * g + 0.131 * b, 0, 255);
        return (nr, ng, nb);
    }
}
