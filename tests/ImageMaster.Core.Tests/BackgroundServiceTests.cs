using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class BackgroundServiceTests
{
    private readonly SkiaBackgroundService _service = new(new FakeAppLogger());

    [Fact]
    public async Task ReplaceBackgroundAsync_SolidColorForTransparent_FillsFullyTransparentPixels()
    {
        // Fully transparent 4x4 source; filling with opaque red should make
        // every pixel opaque red.
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 0);
        var request = new BackgroundReplaceRequest
        {
            Mode = BackgroundReplaceMode.SolidColorForTransparent,
            ArgbFillColor = 0xFFFF0000 // opaque red
        };

        var result = await _service.ReplaceBackgroundAsync(source, request);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        // BGRA layout: byte 2 is Red.
        Assert.Equal(255, pixels[2]);
        Assert.Equal(255, pixels[3]); // alpha now opaque
    }

    [Fact]
    public async Task ReplaceBackgroundAsync_MissingRequiredFillColor_Fails()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 0);
        var request = new BackgroundReplaceRequest { Mode = BackgroundReplaceMode.SolidColorForTransparent, ArgbFillColor = null };

        var result = await _service.ReplaceBackgroundAsync(source, request);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ReplaceBackgroundAsync_ChromaKey_RemovesPixelsMatchingKeyColorExactly()
    {
        // Solid green source, key out pure green with 0% tolerance, fill with blue.
        var source = PixelBufferFactory.CreateSolidColor(4, 4, b: 0, g: 255, r: 0, a: 255);
        var request = new BackgroundReplaceRequest
        {
            Mode = BackgroundReplaceMode.ChromaKeyToSolidColor,
            ChromaKeyArgbColor = 0xFF00FF00, // opaque green
            ChromaKeyTolerancePercent = 0,
            ArgbFillColor = 0xFF0000FF // opaque blue
        };

        var result = await _service.ReplaceBackgroundAsync(source, request);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        // Every pixel should now be blue (BGRA: byte0=B=255, byte2=R=0).
        Assert.Equal(255, pixels[0]);
        Assert.Equal(0, pixels[2]);
    }

    [Fact]
    public async Task ReplaceBackgroundAsync_InvalidTolerance_Fails()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 0, 255, 0, 255);
        var request = new BackgroundReplaceRequest
        {
            Mode = BackgroundReplaceMode.ChromaKeyToSolidColor,
            ChromaKeyArgbColor = 0xFF00FF00,
            ChromaKeyTolerancePercent = 150,
            ArgbFillColor = 0xFF0000FF
        };

        var result = await _service.ReplaceBackgroundAsync(source, request);

        Assert.False(result.Success);
    }
}
