using System.Windows;
using ImageMaster.App.Services;
using ImageMaster.App.ViewModels;
using ImageMaster.App.Views;
using ImageMaster.Core.Interfaces;
using ImageMaster.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ImageMaster.App;

/// <summary>
/// Composition root: builds a Generic Host so every service is registered
/// and resolved through DI, per the app's requirements, rather than
/// constructed ad hoc in code-behind.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        var logger = _host.Services.GetRequiredService<IAppLogger>();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.LogError("Unhandled AppDomain exception.", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError("Unhandled UI-thread exception.", args.Exception);
            System.Windows.MessageBox.Show(
                "An unexpected error occurred and was logged. ImageMaster will try to continue running.",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // Keep the app alive per the "never crash the UI" requirement.
        };

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddImageMasterInfrastructure();

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
