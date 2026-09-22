using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class CropRequestValidationTests
{
    [Fact]
    public void Validate_ZeroOrNegativeDimensions_ReturnsErrors()
    {
        var request = new CropRequest { X = 0, Y = 0, Width = 0, Height = -5 };

        var errors = request.Validate(sourceWidth: 100, sourceHeight: 100);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Validate_NegativePosition_ReturnsError()
    {
        var request = new CropRequest { X = -1, Y = -1, Width = 10, Height = 10 };

        var errors = request.Validate(sourceWidth: 100, sourceHeight: 100);

        Assert.Single(errors);
    }

    [Fact]
    public void Validate_RectangleExtendsPastSourceBounds_ReturnsErrors()
    {
        var request = new CropRequest { X = 90, Y = 90, Width = 50, Height = 50 };

        var errors = request.Validate(sourceWidth: 100, sourceHeight: 100);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Validate_ValidRectangle_ReturnsNoErrors()
    {
        var request = new CropRequest { X = 10, Y = 10, Width = 50, Height = 50 };

        Assert.Empty(request.Validate(sourceWidth: 100, sourceHeight: 100));
    }
}

public class SkiaImageCropServiceTests
{
    private readonly SkiaImageCropService _service = new(new FakeAppLogger());

    [Fact]
    public async Task CropAsync_ProducesBufferWithRequestedDimensions()
    {
        var source = PixelBufferFactory.CreateSolidColor(20, 10, 255, 0, 0, 255);
        var request = new CropRequest { X = 5, Y = 2, Width = 10, Height = 6 };

        var result = await _service.CropAsync(source, request);

        Assert.True(result.Success);
        Assert.Equal(10, result.Value!.Width);
        Assert.Equal(6, result.Value.Height);
    }

    [Fact]
    public async Task CropAsync_RectangleOutsideSource_Fails()
    {
        var source = PixelBufferFactory.CreateSolidColor(10, 10, 0, 0, 0, 255);
        var request = new CropRequest { X = 5, Y = 5, Width = 20, Height = 20 };

        var result = await _service.CropAsync(source, request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CropAsync_PreservesPixelDataFromSourceRegion()
    {
        // Solid red source - after cropping any sub-rectangle, every pixel should still be opaque red.
        var source = PixelBufferFactory.CreateSolidColor(20, 20, 0, 0, 255, 255);
        var request = new CropRequest { X = 4, Y = 4, Width = 8, Height = 8 };

        var result = await _service.CropAsync(source, request);

        Assert.True(result.Success);
        var pixels = result.Value!.Pixels;
        Assert.Equal(255, pixels[2]); // Red
        Assert.Equal(255, pixels[3]); // Alpha
    }
}
