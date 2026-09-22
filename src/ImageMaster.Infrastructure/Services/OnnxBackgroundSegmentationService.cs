using ImageMaster.Core.Interfaces;
using ImageMaster.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace ImageMaster.Infrastructure.Services;

/// <summary>
/// Runs a locally-stored U2Net-family ONNX model (e.g. u2netp.onnx, as
/// distributed by the rembg project: https://github.com/danielgatis/rembg)
/// to predict a foreground/background mask, then applies it as the output
/// image's alpha channel - fully offline, no network calls, no API keys.
/// The model file is a large binary asset and is intentionally not bundled
/// with the app; see <see cref="ModelPath"/> for where it's expected.
/// </summary>
public sealed class OnnxBackgroundSegmentationService : IBackgroundSegmentationService, IDisposable
{
    private const int ModelInputSize = 320;
    private const string InputName = "input.1";
    private const string OutputName = "1959"; // the fused (d0) saliency map - the model exposes 6 intermediate side-outputs too, which are ignored

    private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    private readonly IAppLogger _logger;
    private readonly Lazy<InferenceSession?> _session;

    public OnnxBackgroundSegmentationService(IAppLogger logger)
    {
        _logger = logger;
        ModelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageMaster", "models", "u2netp.onnx");
        _session = new Lazy<InferenceSession?>(LoadSession);
    }

    /// <summary>Where this service looks for the model file.</summary>
    public string ModelPath { get; }

    public bool IsModelAvailable => File.Exists(ModelPath);

    private InferenceSession? LoadSession()
    {
        if (!File.Exists(ModelPath)) return null;
        try
        {
            return new InferenceSession(ModelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load the background-segmentation ONNX model.", ex);
            return null;
        }
    }

    public Task<OperationResult<ImagePixelBuffer>> RemoveBackgroundAsync(ImagePixelBuffer source, CancellationToken cancellationToken = default)
    {
        var session = _session.Value;
        if (session is null)
        {
            return Task.FromResult(OperationResult<ImagePixelBuffer>.Fail(
                "The AI background-removal model isn't installed. Download u2netp.onnx " +
                $"(e.g. from https://github.com/danielgatis/rembg) and place it at:{Environment.NewLine}{ModelPath}"));
        }

        return Task.Run(() =>
        {
            try
            {
                using var sourceBitmap = SkiaBufferConverter.ToSkBitmap(source);
                var inputTensor = BuildInputTensor(sourceBitmap);
                cancellationToken.ThrowIfCancellationRequested();

                using var results = session.Run(
                    new[] { NamedOnnxValue.CreateFromTensor(InputName, inputTensor) },
                    new[] { OutputName });
                var maskTensor = results.First().AsTensor<float>();
                var mask = NormalizeMask(maskTensor);

                cancellationToken.ThrowIfCancellationRequested();
                var pixels = ApplyMaskAsAlpha(source, mask);
                return OperationResult<ImagePixelBuffer>.Ok(new ImagePixelBuffer(source.Width, source.Height, source.Stride, source.Format, pixels));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("AI background removal failed.", ex);
                return OperationResult<ImagePixelBuffer>.Fail("AI background removal failed. The model file may be corrupt or incompatible - see log for details.");
            }
        }, cancellationToken);
    }

    /// <summary>Downscales to the model's fixed 320x320 input and applies standard ImageNet mean/std normalization, as the reference preprocessing for this model does.</summary>
    private static DenseTensor<float> BuildInputTensor(SKBitmap source)
    {
        using var resized = source.Resize(new SKImageInfo(ModelInputSize, ModelInputSize), SKFilterQuality.High);
        var tensor = new DenseTensor<float>(new[] { 1, 3, ModelInputSize, ModelInputSize });

        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var pixel = resized.GetPixel(x, y);
                tensor[0, 0, y, x] = (pixel.Red / 255f - Mean[0]) / Std[0];
                tensor[0, 1, y, x] = (pixel.Green / 255f - Mean[1]) / Std[1];
                tensor[0, 2, y, x] = (pixel.Blue / 255f - Mean[2]) / Std[2];
            }
        }

        return tensor;
    }

    /// <summary>Min-max normalizes the raw 320x320 output to [0,1] - the model doesn't apply a final sigmoid, so this is the standard postprocessing for this architecture.</summary>
    private static float[,] NormalizeMask(Tensor<float> maskTensor)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var v = maskTensor[0, 0, y, x];
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }

        var range = Math.Max(max - min, 1e-6f);
        var mask = new float[ModelInputSize, ModelInputSize];
        for (var y = 0; y < ModelInputSize; y++)
            for (var x = 0; x < ModelInputSize; x++)
                mask[y, x] = (maskTensor[0, 0, y, x] - min) / range;

        return mask;
    }

    /// <summary>Nearest-neighbor-samples the 320x320 mask back up to the source's own dimensions and writes it into the alpha channel - the mask is already soft, so nearest-neighbor upscaling introduces no visible blockiness.</summary>
    private static byte[] ApplyMaskAsAlpha(ImagePixelBuffer source, float[,] mask)
    {
        var pixels = new byte[source.Pixels.Length];
        Buffer.BlockCopy(source.Pixels, 0, pixels, 0, source.Pixels.Length);

        var scaleX = (double)ModelInputSize / source.Width;
        var scaleY = (double)ModelInputSize / source.Height;

        for (var y = 0; y < source.Height; y++)
        {
            var maskY = Math.Clamp((int)(y * scaleY), 0, ModelInputSize - 1);
            var rowOffset = y * source.Stride;
            for (var x = 0; x < source.Width; x++)
            {
                var maskX = Math.Clamp((int)(x * scaleX), 0, ModelInputSize - 1);
                pixels[rowOffset + x * 4 + 3] = (byte)Math.Clamp(mask[maskY, maskX] * 255f, 0, 255);
            }
        }

        return pixels;
    }

    public void Dispose()
    {
        if (_session.IsValueCreated) _session.Value?.Dispose();
    }
}
