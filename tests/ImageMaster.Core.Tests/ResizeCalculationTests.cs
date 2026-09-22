using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class ResizeCalculationTests
{
    private readonly SkiaImageResizeService _service = new(new FakeAppLogger());

    [Fact]
    public void CalculateTargetDimensions_ByPercentage_ScalesBothDimensionsEqually()
    {
        var request = new ResizeRequest { ByPercentage = true, PercentageValue = 50 };

        var (width, height) = _service.CalculateTargetDimensions(800, 600, request);

        Assert.Equal(400, width);
        Assert.Equal(300, height);
    }

    [Fact]
    public void CalculateTargetDimensions_ByDimensions_AspectLockOn_FitsWithinBoxPreservingRatio()
    {
        // Source is 4:3. Requesting 1000x1000 with aspect lock should fit to
        // the smaller dimension (height, since width would overshoot the box).
        var request = new ResizeRequest
        {
            ByPercentage = false,
            TargetWidth = 1000,
            TargetHeight = 1000,
            MaintainAspectRatio = true
        };

        var (width, height) = _service.CalculateTargetDimensions(800, 600, request);

        Assert.Equal(1000, height);
        Assert.True(width < 1000);
        // 800:600 == width:height must still hold (within rounding).
        Assert.InRange(width / (double)height, 800.0 / 600.0 - 0.01, 800.0 / 600.0 + 0.01);
    }

    [Fact]
    public void CalculateTargetDimensions_ByDimensions_AspectLockOff_UsesExactRequestedDimensions()
    {
        var request = new ResizeRequest
        {
            ByPercentage = false,
            TargetWidth = 300,
            TargetHeight = 900,
            MaintainAspectRatio = false
        };

        var (width, height) = _service.CalculateTargetDimensions(800, 600, request);

        Assert.Equal(300, width);
        Assert.Equal(900, height);
    }

    [Fact]
    public void CalculateTargetDimensions_NeverReturnsZeroOrNegativeDimensions()
    {
        var request = new ResizeRequest { ByPercentage = true, PercentageValue = 0.01 };

        var (width, height) = _service.CalculateTargetDimensions(10, 10, request);

        Assert.True(width >= 1);
        Assert.True(height >= 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public async Task ResizeAsync_RejectsInvalidPercentage(double invalidPercent)
    {
        var source = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 255);
        var request = new ResizeRequest { ByPercentage = true, PercentageValue = invalidPercent };

        var result = await _service.ResizeAsync(source, request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ResizeAsync_ProducesBufferWithRequestedDimensions()
    {
        var source = PixelBufferFactory.CreateSolidColor(20, 10, 255, 0, 0, 255);
        var request = new ResizeRequest { ByPercentage = true, PercentageValue = 200 };

        var result = await _service.ResizeAsync(source, request);

        Assert.True(result.Success);
        Assert.Equal(40, result.Value!.Width);
        Assert.Equal(20, result.Value.Height);
    }
}
