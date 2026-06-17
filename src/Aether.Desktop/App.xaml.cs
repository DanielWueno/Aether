using System.Windows;
using Aether.Core.Abstractions;
using Aether.Core.Services;
using Aether.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Aether.Desktop;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ensure the app doesn't exit when the hidden window is "closed" or not shown
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        // Instantiate MainWindow (it contains the TaskbarIcon)
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        
        // We don't call .Show() here because MainWindow.xaml has Visibility="Hidden"
        // and ShowInTaskbar="False", making it effectively a background tray-only window.
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IAdbManager, AdbManager>();
        services.AddSingleton<IProxyManager, ProxyManager>();
        services.AddSingleton<ITetherService, TetherService>();

        // UI - Must be Singleton to keep the tray icon alive
        services.AddSingleton<MainWindow>();
    }
}
