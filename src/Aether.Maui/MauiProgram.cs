using Microsoft.Extensions.Logging;
using Aether.Core.Services;
using Aether.Core.Abstractions;
using Aether.Shared;

namespace Aether.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register Core Services
        builder.Services.AddSingleton<IAdbManager, AdbManager>();
        builder.Services.AddSingleton<IProxyManager, ProxyManager>();
        builder.Services.AddSingleton<ITetherService, TetherService>();

        // Register UI
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
