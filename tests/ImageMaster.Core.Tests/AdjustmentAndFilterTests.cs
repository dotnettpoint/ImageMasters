using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class ImageAdjustmentRequestValidationTests
{
    [Fact]
    public void Validate_AllZero_ReturnsNoErrorsAndIsNoOp()
    {
        var request = new ImageAdjustmentRequest();

        Assert.Empty(request.Validate());
        Assert.True(request.IsNoOp);
    }

    [Theory]
    [InlineData(101, 0, 0, 0)]
    [InlineData(0, -101, 0, 0)]
    [InlineData(0, 0, 200, 0)]
    [InlineData(0, 0, 0, 101)]
    [InlineData(0, 0, 0, -1)]
    public void Validate_OutOfRangeValues_ReturnsError(int brightness, int contrast, int saturation, int sharpen)
    {
        var request = new ImageAdjustmentRequest
        {
            BrightnessPercent = brightness,
            ContrastPercent = contrast,
            SaturationPercent = saturation,
            SharpenAmount = sharpen
        };

        Assert.NotEmpty(request.Validate());
    }

    [Fact]
    public void IsNoOp_AnyNonZeroValue_IsFalse()
    {
        Assert.False(new ImageAdjustmentRequest { BrightnessPercent = 10 }.IsNoOp);
        Assert.False(new ImageAdjustmentRequest { SharpenAmount = 5 }.IsNoOp);
    }
}

public class SkiaImageAdjustmentServiceTests
{
    private readonly SkiaImageAdjustmentService _service = new(new FakeAppLogger());

    [Fact]
    public async Task ApplyAdjustmentsAsync_PositiveBrightness_LightensPixels()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 100, 100, 100, 255);
        var request = new ImageAdjustmentRequest { BrightnessPercent = 50 };

        var result = await _service.ApplyAdjustmentsAsync(source, request);

        Assert.True(result.Success);
        Assert.True(result.Value!.Pixels[0] > source.Pixels[0]); // B channel brighter
    }

    [Fact]
    public async Task ApplyAdjustmentsAsync_NegativeBrightness_DarkensPixels()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 200, 200, 200, 255);
        var request = new ImageAdjustmentRequest { BrightnessPercent = -50 };

        var result = await _service.ApplyAdjustmentsAsync(source, request);

        Assert.True(result.Success);
        Assert.True(result.Value!.Pixels[0] < source.Pixels[0]);
    }

    [Fact]
    public async Task ApplyAdjustmentsAsync_FullyDesaturated_ProducesEqualRgbChannels()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 20, 100, 220, 255); // saturated blue-ish
        var request = new ImageAdjustmentRequest { SaturationPercent = -100 };

        var result = await _service.ApplyAdjustmentsAsync(source, request);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        Assert.Equal(pixels[0], pixels[1]); // B == G
        Assert.Equal(pixels[1], pixels[2]); // G == R
    }

    [Fact]
    public async Task ApplyAdjustmentsAsync_AllZero_LeavesPixelsUnchanged()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 77, 88, 99, 255);
        var request = new ImageAdjustmentRequest();

        var result = await _service.ApplyAdjustmentsAsync(source, request);

        Assert.True(result.Success);
        Assert.Equal(source.Pixels, result.Value!.Pixels);
    }

    [Fact]
    public async Task ApplyAdjustmentsAsync_InvalidRequest_Fails()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 255);
        var request = new ImageAdjustmentRequest { BrightnessPercent = 500 };

        var result = await _service.ApplyAdjustmentsAsync(source, request);

        Assert.False(result.Success);
    }
}

public class SkiaImageFilterServiceTests
{
    private readonly SkiaImageFilterService _service = new(new FakeAppLogger());

    [Fact]
    public async Task ApplyFilterAsync_Grayscale_ProducesEqualRgbChannels()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 10, 150, 240, 255);

        var result = await _service.ApplyFilterAsync(source, ImageFilterType.Grayscale);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[1], pixels[2]);
    }

    [Fact]
    public async Task ApplyFilterAsync_Invert_FlipsEachChannel()
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 10, 20, 30, 255);

        var result = await _service.ApplyFilterAsync(source, ImageFilterType.Invert);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        Assert.Equal(245, pixels[0]); // 255-10
        Assert.Equal(235, pixels[1]); // 255-20
        Assert.Equal(225, pixels[2]); // 255-30
        Assert.Equal(255, pixels[3]); // alpha untouched
    }

    [Fact]
    public async Task ApplyFilterAsync_Sepia_ProducesWarmTone()
    {
        // Neutral mid-gray in, sepia should push toward a warm (R >= G >= B) tone.
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 128, 128, 128, 255);

        var result = await _service.ApplyFilterAsync(source, ImageFilterType.Sepia);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        byte b = pixels[0], g = pixels[1], r = pixels[2];
        Assert.True(r >= g);
        Assert.True(g >= b);
    }
}
