# ImageMaster

A Windows desktop photo viewer/editor (.NET 8, WPF) built to open image files
that Microsoft Photos refuses to open (corrupted EXIF/ICC metadata,
non-standard color profiles) but that Paint can still open.

> **Build note:** This solution was generated and written without access to
> a .NET SDK or a Windows build environment, so it has **not** been compiled
> or run. Everything was written carefully against the real SkiaSharp/WPF
> APIs, but please run a build (`dotnet build`) as your first step and treat
> any compiler errors as expected teething issues to fix, not a sign
> something is fundamentally wrong with the design. The most likely trouble
> spots are called out below.

## Solution structure

```
ImageMaster.sln
src/
  ImageMaster.Core/            Models + interfaces only. No SkiaSharp, no WPF.
  ImageMaster.Infrastructure/  SkiaSharp-based service implementations.
  ImageMaster.App/             WPF UI (MVVM) + composition root.
tests/
  ImageMaster.Core.Tests/      xUnit tests for the core editing logic.
```

Clean Architecture layering: `App` depends on `Infrastructure` and `Core`;
`Infrastructure` depends on `Core`; `Core` depends on nothing. All cross-layer
contracts (`IImageLoaderService`, `IImageResizeService`, `IDpiService`,
`ITextOverlayService`, `IBackgroundService`, `IFileExportService`,
`IAppLogger`) live in Core, so Infrastructure is swappable and the App layer
is testable against fakes.

## Why SkiaSharp opens files Photos can't

Microsoft Photos decodes via WIC (Windows Imaging Component), which is
strict about malformed metadata segments and will refuse to open a file
whose EXIF/ICC block is corrupt, even though the pixel data is perfectly
fine. SkiaSharp's decoder (built on Skia/libjpeg-turbo) is far more
tolerant - it decodes pixels first and treats metadata as optional,
which is exactly the behavior Paint also exhibits. `SkiaImageLoaderService`
leans into this by decoding pixels and reading metadata as two fully
separate, separately-caught steps (see `LoadAsync`): a metadata failure
never blocks the image from opening, it only adds a warning.

## NuGet packages and why

| Package | Used for |
|---|---|
| `SkiaSharp` + `SkiaSharp.NativeAssets.Win32` | Cross-platform image decode/encode/draw. Chosen over `System.Drawing.Common` per the requirements (GDI+-based, Windows-only, effectively legacy). |
| `MetadataExtractor` | Defensive EXIF/JFIF/ICC metadata reading, isolated from pixel decode. |
| `Microsoft.Extensions.Hosting` / `...DependencyInjection` | Generic Host as the DI composition root (`App.xaml.cs`), so every service is constructor-injected rather than hand-wired. |
| `Microsoft.ML.OnnxRuntime` | Runs locally-stored ONNX models offline for the AI features (see below). |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | Standard xUnit test stack. |

## Known limitations (called out in code comments too)

- **TIFF export isn't supported.** SkiaSharp has no TIFF encoder. `SkiaFileExportService`
  surfaces this as a clean `OperationResult` failure ("TIFF export isn't
  supported by the current image codec on this system") rather than
  crashing. TIFF *opening* still works fine, since Skia can decode more
  formats than it can encode. A production build could add a TIFF encoder
  via a native `libtiff` binding if that format is a hard requirement.
- **Chroma-key background removal** (`SkiaBackgroundService`) still exists
  alongside the AI option below, for a uniform backdrop (green screen, flat
  studio background) where it's faster and needs no model file.
- **Undo/redo is snapshot-based**, not command/diff-based (see
  `UndoRedoStack<T>` in `App/Infrastructure`). Simple and correct, but each
  undo step holds a full copy of the pixel buffer; fine for typical photo
  sizes and a bounded history (default: 20 steps), but not ideal for very
  large images or very long editing sessions.
- **File-size estimate before saving is a heuristic** (`SkiaDpiService.EstimateFileSizeBytes`),
  based on format/quality-dependent bytes-per-pixel constants. Actual
  encoders make content-dependent choices, so treat it as a ballpark, not
  an exact prediction.

## Things worth double-checking on first build

Since this couldn't be compiled: the two spots most likely to need a small
fix are (1) `MetadataExtractor`'s exact API surface for reading
`ExifIfd0Directory`/`JfifDirectory` resolution tags in
`SkiaImageLoaderService.ReadResolutionTags` - the tag constant names are
correct as of the package version pinned in the `.csproj`, but metadata
library APIs do shift between versions - and (2) `SKBitmap.Resize`'s exact
overload/return-null semantics, which can vary slightly across SkiaSharp
versions.

## AI features (local ONNX models)

AI-based features run fully offline via `Microsoft.ML.OnnxRuntime` - no
network calls at runtime, no API keys, no per-use cost. Model weights are
large binary assets and are **never checked into this repo** (see
`.gitignore`); each one is downloaded once per machine into
`%LOCALAPPDATA%\ImageMaster\models\`.

| Feature | Status | Model | Where to get it |
|---|---|---|---|
| AI Remove Background | Implemented (`OnnxBackgroundSegmentationService`) | `u2netp.onnx` (~4.6 MB, U2Net-family saliency segmentation) | https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx |
| Object removal (inpainting) | Not yet implemented | - | - |
| AI image editing / enhancement | Not yet implemented | - | - |

To enable AI background removal, download the file above and save it as
`%LOCALAPPDATA%\ImageMaster\models\u2netp.onnx`. Without it, the "AI Remove
Background" command shows a clear in-app error naming the exact expected
path instead of failing silently or crashing. Once the background is
transparent, use the existing "Change Background..." dialog (Image menu) to
fill it with a color or composite it over a replacement image - the AI step
only produces the mask; compositing reuses the pipeline already built for
chroma-key mode.

## Running

```
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ImageMaster.App
```

## Keyboard shortcuts

`Ctrl+O` Open · `Ctrl+S` Save As · `Ctrl+Z` Undo · `Ctrl+Y` Redo
