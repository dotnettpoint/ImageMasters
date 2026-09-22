using ImageMaster.Core.Models;
using ImageMaster.Core.Tests.TestDoubles;
using ImageMaster.Infrastructure.Services;
using Xunit;

namespace ImageMaster.Core.Tests;

/// <summary>
/// These tests exercise the real ONNX model when it's present at the
/// service's expected local path, so they can only run on a machine that
/// has downloaded it - see OnnxBackgroundSegmentationService's XML docs for
/// where to get it. They pass trivially (not fail) when the model is
/// absent, since the model is a large binary asset intentionally not
/// checked into source control.
/// </summary>
public class OnnxBackgroundSegmentationServiceTests
{
    private readonly OnnxBackgroundSegmentationService _service = new(new FakeAppLogger());

    [Fact]
    public void IsModelAvailable_ReflectsWhetherTheModelFileExists()
    {
        Assert.Equal(File.Exists(_service.ModelPath), _service.IsModelAvailable);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_ModelMissing_FailsWithActionableMessage()
    {
        if (_service.IsModelAvailable) return; // covered by the "model present" test instead

        var source = PixelBufferFactory.CreateSolidColor(16, 16, 255, 255, 255, 255);
        var result = await _service.RemoveBackgroundAsync(source);

        Assert.False(result.Success);
        Assert.Contains(_service.ModelPath, result.ErrorMessage);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_ModelPresent_ProducesVaryingAlphaWithRgbUnchanged()
    {
        if (!_service.IsModelAvailable) return; // model not downloaded on this machine - see class doc comment

        // A bright square "subject" on a dark background - not a real photo,
        // but enough to confirm the pipeline runs end-to-end and produces a
        // real (non-constant) mask rather than silently no-op'ing.
        var width = 200;
        var height = 200;
        var source = PixelBufferFactory.CreateSolidColor(width, height, 20, 20, 20, 255);
        for (var y = 60; y < 140; y++)
        {
            var rowOffset = y * source.Stride;
            for (var x = 60; x < 140; x++)
            {
                var offset = rowOffset + x * 4;
                source.Pixels[offset] = 240;     // B
                source.Pixels[offset + 1] = 240; // G
                source.Pixels[offset + 2] = 240; // R
            }
        }

        var result = await _service.RemoveBackgroundAsync(source);

        Assert.True(result.Success);
        var output = result.Value!;
        Assert.Equal(width, output.Width);
        Assert.Equal(height, output.Height);

        // RGB must be untouched - only alpha should change.
        for (var i = 0; i < output.Pixels.Length; i += 4)
        {
            Assert.Equal(source.Pixels[i], output.Pixels[i]);
            Assert.Equal(source.Pixels[i + 1], output.Pixels[i + 1]);
            Assert.Equal(source.Pixels[i + 2], output.Pixels[i + 2]);
        }

        var distinctAlphaValues = new HashSet<byte>();
        for (var i = 3; i < output.Pixels.Length; i += 4)
            distinctAlphaValues.Add(output.Pixels[i]);

        Assert.True(distinctAlphaValues.Count > 1, "Expected the predicted mask to vary across the image, not be a constant value.");
    }
}
