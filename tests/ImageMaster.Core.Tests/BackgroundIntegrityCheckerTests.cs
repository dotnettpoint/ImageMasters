using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class BackgroundIntegrityCheckerTests
{
    private readonly BackgroundIntegrityChecker _checker = new();

    [Fact]
    public void CheckBackgroundColor_IdenticalImages_ReturnsNoWarnings()
    {
        var before = PixelBufferFactory.CreateSolidColor(20, 20, 255, 255, 255, 255);
        var after = PixelBufferFactory.CreateSolidColor(20, 20, 255, 255, 255, 255);

        var result = _checker.CheckBackgroundColor(before, after);

        Assert.True(result.IsWithinTolerance);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void CheckBackgroundColor_SmallShiftWithinTolerance_ReturnsNoWarnings()
    {
        var before = PixelBufferFactory.CreateSolidColor(20, 20, 255, 255, 255, 255);
        var after = PixelBufferFactory.CreateSolidColor(20, 20, 250, 250, 250, 255); // 5 off, default tolerance is 10

        var result = _checker.CheckBackgroundColor(before, after);

        Assert.True(result.IsWithinTolerance);
    }

    [Fact]
    public void CheckBackgroundColor_ShiftBeyondTolerance_ReturnsWarningsForEveryCorner()
    {
        // Fully white background silently shifted to black - the classic
        // "codec dropped alpha and picked black" failure mode.
        var before = PixelBufferFactory.CreateSolidColor(20, 20, 255, 255, 255, 255);
        var after = PixelBufferFactory.CreateSolidColor(20, 20, 0, 0, 0, 255);

        var result = _checker.CheckBackgroundColor(before, after);

        Assert.False(result.IsWithinTolerance);
        Assert.Equal(4, result.Warnings.Count); // all four corners shifted
    }

    [Fact]
    public void CheckBackgroundColor_CustomTolerance_IsRespected()
    {
        var before = PixelBufferFactory.CreateSolidColor(20, 20, 200, 200, 200, 255);
        var after = PixelBufferFactory.CreateSolidColor(20, 20, 210, 210, 210, 255); // 10 off

        Assert.True(_checker.CheckBackgroundColor(before, after, toleranceRgb: 20).IsWithinTolerance);
        Assert.False(_checker.CheckBackgroundColor(before, after, toleranceRgb: 5).IsWithinTolerance);
    }

    // --- Integration: run a solid-background image through each real editing operation ---

    [Fact]
    public async Task Resize_OnSolidBackgroundImage_LeavesBackgroundColorUnchanged()
    {
        var source = PixelBufferFactory.CreateSolidColor(40, 40, 255, 255, 255, 255);
        var resizeService = new SkiaImageResizeService(new FakeAppLogger());
        var request = new ResizeRequest { ByPercentage = true, PercentageValue = 50 };

        var resizeResult = await resizeService.ResizeAsync(source, request);
        Assert.True(resizeResult.Success);

        var integrity = _checker.CheckBackgroundColor(source, resizeResult.Value!);
        Assert.True(integrity.IsWithinTolerance);
    }

    [Fact]
    public async Task Crop_OnSolidBackgroundImage_LeavesBackgroundColorUnchanged()
    {
        var source = PixelBufferFactory.CreateSolidColor(40, 40, 255, 255, 255, 255);
        var cropService = new SkiaImageCropService(new FakeAppLogger());
        var request = new CropRequest { X = 5, Y = 5, Width = 20, Height = 20 };

        var cropResult = await cropService.CropAsync(source, request);
        Assert.True(cropResult.Success);

        var integrity = _checker.CheckBackgroundColor(source, cropResult.Value!);
        Assert.True(integrity.IsWithinTolerance);
    }

    [Fact]
    public async Task TextOverlay_CenteredAwayFromCorners_LeavesBackgroundColorUnchanged()
    {
        var source = PixelBufferFactory.CreateSolidColor(100, 100, 255, 255, 255, 255);
        var textService = new SkiaTextOverlayService(new FakeAppLogger());
        var layers = new List<TextOverlayLayer>
        {
            new() { Text = "Hi", X = 40, Y = 40, FontSizePoints = 12, ArgbColor = 0xFF000000 }
        };

        var textResult = await textService.ApplyTextLayersAsync(source, layers);
        Assert.True(textResult.Success);

        var integrity = _checker.CheckBackgroundColor(source, textResult.Value!);
        Assert.True(integrity.IsWithinTolerance);
    }
}
