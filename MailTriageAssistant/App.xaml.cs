using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using MailTriageAssistant.Services;
using MailTriageAssistant.ViewModels;

namespace MailTriageAssistant;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Never surface raw exception messages that could contain email content.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<RedactionService>();
        services.AddSingleton<ClipboardSecurityHelper>();
        services.AddSingleton<IOutlookService, OutlookService>();
        services.AddSingleton<TriageService>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<TemplateService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(
            "예기치 않은 오류가 발생했습니다. Outlook 상태를 확인한 뒤 다시 시도해 주세요.",
            "MailTriageAssistant",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
