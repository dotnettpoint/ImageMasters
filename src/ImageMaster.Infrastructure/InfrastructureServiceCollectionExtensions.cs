using ImageMaster.Core.Interfaces;
using ImageMaster.Infrastructure.Logging;
using ImageMaster.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageMaster.Infrastructure;

/// <summary>Registers all Infrastructure-layer service implementations against their Core interfaces.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddImageMasterInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppLogger, FileAppLogger>();
        services.AddSingleton<IImageLoaderService, SkiaImageLoaderService>();
        services.AddSingleton<IImageResizeService, SkiaImageResizeService>();
        services.AddSingleton<IImageTransformService, SkiaImageTransformService>();
        services.AddSingleton<IDpiService, SkiaDpiService>();
        services.AddSingleton<ITextOverlayService, SkiaTextOverlayService>();
        services.AddSingleton<IBackgroundService, SkiaBackgroundService>();
        services.AddSingleton<IFileExportService, SkiaFileExportService>();
        return services;
    }
}
