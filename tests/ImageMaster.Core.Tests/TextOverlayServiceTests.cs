using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class TextOverlayServiceTests
{
    private readonly SkiaTextOverlayService _service = new(new FakeAppLogger());

    [Fact]
    public async Task ApplyTextLayersAsync_WithNoLayers_ReturnsUnchangedCopy()
    {
        var source = PixelBufferFactory.CreateSolidColor(10, 10, 0, 0, 0, 255);

        var result = await _service.ApplyTextLayersAsync(source, Array.Empty<TextOverlayLayer>());

        Assert.True(result.Success);
        Assert.Equal(source.Width, result.Value!.Width);
        Assert.Equal(source.Height, result.Value.Height);
    }

    [Fact]
    public async Task ApplyTextLayersAsync_PreservesSourceBufferDimensions()
    {
        var source = PixelBufferFactory.CreateSolidColor(64, 32, 0, 0, 0, 255);
        var layer = new TextOverlayLayer { Text = "Hi", X = 5, Y = 5, FontSizePoints = 12 };

        var result = await _service.ApplyTextLayersAsync(source, new[] { layer });

        Assert.True(result.Success);
        Assert.Equal(64, result.Value!.Width);
        Assert.Equal(32, result.Value.Height);
    }

    [Fact]
    public async Task ApplyTextLayersAsync_ActuallyChangesPixelsUnderTheText()
    {
        // Solid black background, opaque white text near the top-left -
        // the pixels around the text origin should no longer be pure black.
        var source = PixelBufferFactory.CreateSolidColor(80, 40, 0, 0, 0, 255);
        var layer = new TextOverlayLayer
        {
            Text = "AB",
            X = 2,
            Y = 2,
            FontSizePoints = 24,
            ArgbColor = 0xFFFFFFFF
        };

        var result = await _service.ApplyTextLayersAsync(source, new[] { layer });

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        var hasNonBlackPixel = false;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0)
            {
                hasNonBlackPixel = true;
                break;
            }
        }

        Assert.True(hasNonBlackPixel, "Expected at least one pixel to be altered by the text overlay.");
    }

    [Fact]
    public async Task ApplyTextLayersAsync_RejectsEmptyText()
    {
        var source = PixelBufferFactory.CreateSolidColor(10, 10, 0, 0, 0, 255);
        var layer = new TextOverlayLayer { Text = "" };

        var result = await _service.ApplyTextLayersAsync(source, new[] { layer });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyTextLayersAsync_RejectsOutOfRangeOpacity()
    {
        var source = PixelBufferFactory.CreateSolidColor(10, 10, 0, 0, 0, 255);
        var layer = new TextOverlayLayer { Text = "Hi", OpacityPercent = 150 };

        var result = await _service.ApplyTextLayersAsync(source, new[] { layer });

        Assert.False(result.Success);
    }
}
