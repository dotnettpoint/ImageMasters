using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace ImageMaster.Core.Tests;

public class FileExportServiceTests : IDisposable
{
    private readonly SkiaFileExportService _service = new(new FakeAppLogger());
    private readonly string _tempDirectory;

    public FileExportServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ImageMasterTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void ValidateOutputPath_SameAsOriginal_WithoutOverwriteFlag_IsRejected()
    {
        var original = Path.Combine(_tempDirectory, "photo.jpg");

        var errors = _service.ValidateOutputPath(original, original, allowOverwriteOriginal: false);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateOutputPath_SameAsOriginal_WithOverwriteFlag_IsAllowed()
    {
        var original = Path.Combine(_tempDirectory, "photo.jpg");

        var errors = _service.ValidateOutputPath(original, original, allowOverwriteOriginal: true);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateOutputPath_DifferentFileName_IsAllowedWithoutOverwriteFlag()
    {
        var original = Path.Combine(_tempDirectory, "photo.jpg");
        var newPath = Path.Combine(_tempDirectory, "photo-edited.jpg");

        var errors = _service.ValidateOutputPath(newPath, original, allowOverwriteOriginal: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateOutputPath_NonexistentDirectory_IsRejected()
    {
        var newPath = Path.Combine(_tempDirectory, "does-not-exist", "photo.jpg");

        var errors = _service.ValidateOutputPath(newPath, null, allowOverwriteOriginal: false);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task SaveAsAsync_WritesPngFileToDisk()
    {
        var buffer = PixelBufferFactory.CreateSolidColor(8, 8, 255, 0, 0, 255);
        var outputPath = Path.Combine(_tempDirectory, "output.png");
        var request = new ExportRequest { OutputFilePath = outputPath, TargetFormat = ImageFormatType.Png };

        var result = await _service.SaveAsAsync(buffer, new ImageMetadata(), originalFilePath: null, request);

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task SaveAsAsync_TiffFormat_FailsGracefullyWithClearMessage()
    {
        // SkiaSharp has no TIFF encoder; this must surface as a clean error,
        // not an unhandled exception.
        var buffer = PixelBufferFactory.CreateSolidColor(8, 8, 0, 0, 0, 255);
        var outputPath = Path.Combine(_tempDirectory, "output.tiff");
        var request = new ExportRequest { OutputFilePath = outputPath, TargetFormat = ImageFormatType.Tiff };

        var result = await _service.SaveAsAsync(buffer, new ImageMetadata(), originalFilePath: null, request);

        Assert.False(result.Success);
        Assert.False(File.Exists(outputPath));
        Assert.Contains("TIFF", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsAsync_OverwritingOriginalWithoutFlag_IsRejected()
    {
        var originalPath = Path.Combine(_tempDirectory, "original.png");
        await File.WriteAllBytesAsync(originalPath, new byte[] { 1, 2, 3 });

        var buffer = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 255);
        var request = new ExportRequest { OutputFilePath = originalPath, TargetFormat = ImageFormatType.Png, AllowOverwriteOriginal = false };

        var result = await _service.SaveAsAsync(buffer, new ImageMetadata(), originalPath, request);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SaveAsAsync_TransparentBufferSavedAsJpeg_CompositesOntoWhiteByDefault()
    {
        // Fully transparent black - if the codec dropped alpha naively it
        // could resolve to black; it must resolve to the default background
        // (white) instead, per the background-integrity requirement.
        var buffer = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 0);
        var outputPath = Path.Combine(_tempDirectory, "transparent-on-white.jpg");
        var request = new ExportRequest { OutputFilePath = outputPath, TargetFormat = ImageFormatType.Jpeg };

        var result = await _service.SaveAsAsync(buffer, new ImageMetadata(), originalFilePath: null, request);

        Assert.True(result.Success);
        using var decoded = SKBitmap.Decode(outputPath);
        var pixel = decoded.GetPixel(0, 0);
        Assert.True(pixel.Red > 240 && pixel.Green > 240 && pixel.Blue > 240, $"Expected a near-white background, got {pixel}.");
    }

    [Fact]
    public async Task SaveAsAsync_TransparentBufferSavedAsJpeg_WithCustomBackground_UsesThatColor()
    {
        var buffer = PixelBufferFactory.CreateSolidColor(4, 4, 0, 0, 0, 0);
        var outputPath = Path.Combine(_tempDirectory, "transparent-on-red.jpg");
        var request = new ExportRequest
        {
            OutputFilePath = outputPath,
            TargetFormat = ImageFormatType.Jpeg,
            BackgroundFillArgb = 0xFFFF0000 // opaque red
        };

        var result = await _service.SaveAsAsync(buffer, new ImageMetadata(), originalFilePath: null, request);

        Assert.True(result.Success);
        using var decoded = SKBitmap.Decode(outputPath);
        var pixel = decoded.GetPixel(0, 0);
        Assert.True(pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60, $"Expected a near-red background, got {pixel}.");
    }

    [Fact]
    public async Task SaveAsAsync_OpaqueBufferSavedAsJpeg_PixelsUnaffectedByFlatteningPath()
    {
        var buffer = PixelBufferFactory.CreateSolidColor(4, 4, 10, 20, 30, 255);
        var outputPath = Path.Combine(_tempDirectory, "opaque.jpg");
        var request = new ExportRequest { OutputFilePath = outputPath, TargetFormat = ImageFormatType.Jpeg };

        var result = await _service.SaveAsAsync(buffer, new ImageMetadata(), originalFilePath: null, request);

        Assert.True(result.Success);
        using var decoded = SKBitmap.Decode(outputPath);
        var pixel = decoded.GetPixel(0, 0);
        // JPEG is lossy, so allow a small tolerance rather than an exact match.
        Assert.InRange(pixel.Red, 20, 40);
        Assert.InRange(pixel.Green, 10, 30);
        Assert.InRange(pixel.Blue, 0, 20);
    }
}
