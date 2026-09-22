using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

public class DpiServiceTests
{
    private readonly SkiaDpiService _service = new();

    [Theory]
    [InlineData(72, 72)]
    [InlineData(96, 96)]
    [InlineData(300, 300)]
    public void ChangeDpi_WithValidValues_ReturnsUpdatedMetadata(double dpiX, double dpiY)
    {
        var original = new ImageMetadata { DpiX = 96, DpiY = 96 };
        var request = new DpiChangeRequest { DpiX = dpiX, DpiY = dpiY };

        var result = _service.ChangeDpi(original, request);

        Assert.True(result.Success);
        Assert.Equal(dpiX, result.Value!.DpiX);
        Assert.Equal(dpiY, result.Value.DpiY);
    }

    [Fact]
    public void ChangeDpi_PreservesExifAndIccData()
    {
        var original = new ImageMetadata
        {
            DpiX = 96,
            DpiY = 96,
            ExifTags = new Dictionary<string, string> { ["Make"] = "TestCam" },
            IccProfile = new byte[] { 1, 2, 3 }
        };

        var result = _service.ChangeDpi(original, new DpiChangeRequest { DpiX = 300, DpiY = 300 });

        Assert.True(result.Success);
        Assert.Equal("TestCam", result.Value!.ExifTags["Make"]);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Value.IccProfile);
    }

    [Theory]
    [InlineData(0, 96)]
    [InlineData(96, 0)]
    [InlineData(-10, 96)]
    [InlineData(2500, 96)]
    public void ChangeDpi_RejectsOutOfRangeValues(double dpiX, double dpiY)
    {
        var result = _service.ChangeDpi(new ImageMetadata(), new DpiChangeRequest { DpiX = dpiX, DpiY = dpiY });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void EstimateFileSizeBytes_NeverReturnsBelowOneKilobyte()
    {
        var tinyBuffer = PixelBufferFactory.CreateSolidColor(1, 1, 0, 0, 0, 255);
        var request = new ExportRequest { OutputFilePath = "x.jpg", TargetFormat = ImageFormatType.Jpeg, JpegQuality = 1 };

        var estimate = _service.EstimateFileSizeBytes(tinyBuffer, request);

        Assert.True(estimate >= 1024);
    }

    [Fact]
    public void EstimateFileSizeBytes_HigherJpegQualityEstimatesLargerFile()
    {
        var buffer = PixelBufferFactory.CreateSolidColor(500, 500, 0, 0, 0, 255);
        var lowQuality = new ExportRequest { OutputFilePath = "x.jpg", TargetFormat = ImageFormatType.Jpeg, JpegQuality = 10 };
        var highQuality = new ExportRequest { OutputFilePath = "x.jpg", TargetFormat = ImageFormatType.Jpeg, JpegQuality = 95 };

        var lowEstimate = _service.EstimateFileSizeBytes(buffer, lowQuality);
        var highEstimate = _service.EstimateFileSizeBytes(buffer, highQuality);

        Assert.True(highEstimate > lowEstimate);
    }
}
