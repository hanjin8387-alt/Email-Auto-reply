using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MailTriageAssistant;

public static class AppBootstrapper
{
    public static AppBootstrapContext Initialize()
    {
        var services = new ServiceCollection();
        AppServiceRegistration.ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        AppLocalizationManager.TryApplyLanguageResources(serviceProvider);

        var appLogger = serviceProvider.GetRequiredService<ILogger<App>>();
        appLogger.LogInformation("App started.");

        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        return new AppBootstrapContext(serviceProvider, appLogger, mainWindow);
    }
}

public sealed record AppBootstrapContext(
    ServiceProvider ServiceProvider,
    ILogger<App> Logger,
    MainWindow MainWindow);
