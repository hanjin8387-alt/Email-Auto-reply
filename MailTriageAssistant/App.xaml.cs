using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using MailTriageAssistant.ViewModels;
using Serilog;
using Serilog.Events;

namespace MailTriageAssistant;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;

    internal static bool IsExitRequested { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Never surface raw exception messages that could contain email content.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        TryApplyUserVipOverrides(_serviceProvider);
        TryApplyLanguageResources(_serviceProvider);

        _serviceProvider.GetRequiredService<ILogger<App>>()
            .LogInformation("App started.");

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        TryInitializeSystemTray(mainWindow);

        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _trayIcon?.Dispose();
        }
        catch
        {
            // Ignore tray disposal issues.
        }
        _trayIcon = null;

        try
        {
            var stats = _serviceProvider?.GetService<SessionStatsService>();
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            if (stats is not null && logger is not null)
            {
                var snapshot = stats.Snapshot();
                logger.LogInformation(
                    "Session stats: HeadersLoaded={HeadersLoaded}, DigestsGenerated={DigestsGenerated}, DigestsCopied={DigestsCopied}, TeamsOpenAttempts={TeamsOpenAttempts}, Errors={Errors}.",
                    snapshot.HeadersLoaded,
                    snapshot.DigestsGenerated,
                    snapshot.DigestsCopied,
                    snapshot.TeamsOpenAttempts,
                    snapshot.Errors);
            }
        }
        catch
        {
            // Ignore stats/logging failures on shutdown.
        }

        _serviceProvider?.Dispose();
        _serviceProvider = null;

        Log.CloseAndFlush();

        base.OnExit(e);
    }

    private void TryInitializeSystemTray(MainWindow mainWindow)
    {
        try
        {
            var config = _serviceProvider?.GetService<IConfiguration>();
            if (config is null)
            {
                return;
            }

            var enabled = config.GetValue("TriageSettings:EnableSystemTray", defaultValue: true);
            if (!enabled)
            {
                return;
            }

            if (mainWindow.DataContext is not MainViewModel vm)
            {
                return;
            }

            InitializeSystemTray(mainWindow, vm);
        }
        catch
        {
            // Ignore system tray initialization failures; app should still run.
        }
    }

    private void InitializeSystemTray(MainWindow mainWindow, MainViewModel viewModel)
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var statusItem = new MenuItem { IsEnabled = false };
        void UpdateStatus()
        {
            statusItem.Header = GetResourceString(viewModel.IsLoading
                ? "Str.Tray.StatusProcessing"
                : "Str.Tray.StatusIdle");
        }

        UpdateStatus();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.Equals(args.PropertyName, nameof(MainViewModel.IsLoading), StringComparison.Ordinal))
            {
                return;
            }

            mainWindow.Dispatcher.Invoke(UpdateStatus);
        };

        var menu = new ContextMenu();
        menu.Items.Add(statusItem);
        menu.Items.Add(new Separator());

        var runTriage = new MenuItem { Header = GetResourceString("Str.Button.LoadEmails") };
        runTriage.Click += (_, _) => ExecuteCommand(viewModel.LoadEmailsCommand);

        var copyDigest = new MenuItem { Header = GetResourceString("Str.Button.GenerateDigestTeams") };
        copyDigest.Click += (_, _) => ExecuteCommand(viewModel.GenerateDigestCommand);

        var open = new MenuItem { Header = GetResourceString("Str.Tray.OpenDashboard") };
        open.Click += (_, _) => ShowMainWindow(mainWindow);

        var exit = new MenuItem { Header = GetResourceString("Str.Tray.Exit") };
        exit.Click += (_, _) =>
        {
            IsExitRequested = true;
            Shutdown();
        };

        menu.Items.Add(runTriage);
        menu.Items.Add(copyDigest);
        menu.Items.Add(new Separator());
        menu.Items.Add(open);
        menu.Items.Add(exit);

        _trayIcon = new TaskbarIcon
        {
            Icon = SystemIcons.Application,
            ToolTipText = GetResourceString("Str.MainWindow.Title"),
            ContextMenu = menu,
            Visibility = Visibility.Visible,
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow(mainWindow);
    }

    private static string GetResourceString(string key)
    {
        try
        {
            return (Current?.TryFindResource(key) as string) ?? key;
        }
        catch
        {
            return key;
        }
    }

    private static void ExecuteCommand(ICommand command)
    {
        try
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
        catch
        {
            // Ignore command failures from tray.
        }
    }

    private static void ShowMainWindow(MainWindow mainWindow)
    {
        try
        {
            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }

            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Activate();
        }
        catch
        {
            // Ignore window show failures.
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<TriageSettings>(configuration.GetSection(nameof(TriageSettings)));

        ConfigureLogging(services);

        services.AddSingleton<SessionStatsService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<RedactionService>();
        services.AddSingleton<IRedactionService>(sp => sp.GetRequiredService<RedactionService>());
        services.AddSingleton<ClipboardSecurityHelper>();
        services.AddSingleton<IOutlookService, OutlookService>();
        services.AddSingleton<TriageService>();
        services.AddSingleton<ITriageService>(sp => sp.GetRequiredService<TriageService>());
        services.AddSingleton<DigestService>();
        services.AddSingleton<IDigestService>(sp => sp.GetRequiredService<DigestService>());
        services.AddSingleton<TemplateService>();
        services.AddSingleton<ITemplateService>(sp => sp.GetRequiredService<TemplateService>());

        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MailTriageAssistant",
            "logs");

        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "MailTriageAssistant-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        try
        {
            // Never log exception messages: they could include email-derived content. Type/HResult only.
            var app = sender as App;
            var logger = app?._serviceProvider?.GetService<ILogger<App>>();
            var ex = e.Exception;
            if (logger is not null && ex is not null)
            {
                logger.LogError(
                    "Unhandled exception: {ExceptionType} (HResult={HResult}).",
                    ex.GetType().Name,
                    ex.HResult);
            }
        }
        catch
        {
            // Ignore logging failures inside exception handler.
        }

        // Use IDialogService to avoid direct MessageBox calls (testability + single UI abstraction).
        var dialog = (sender as App)?._serviceProvider?.GetService<IDialogService>()
            ?? new WpfDialogService();

        dialog.ShowError(
            "예기치 않은 오류가 발생했습니다. Outlook 상태를 확인한 뒤 다시 시도해 주세요.",
            "MailTriageAssistant");
    }

    private static void TryApplyUserVipOverrides(IServiceProvider services)
    {
        try
        {
            var settings = services.GetService<ISettingsService>();
            if (settings is null)
            {
                return;
            }

            var vip = settings.LoadVipSendersAsync().GetAwaiter().GetResult();
            if (vip.Count <= 0)
            {
                return;
            }

            var triageOptions = services.GetService<IOptionsMonitor<TriageSettings>>();
            if (triageOptions?.CurrentValue is null)
            {
                return;
            }

            triageOptions.CurrentValue.VipSenders = vip.ToArray();
        }
        catch
        {
            // Ignore user settings load failures; app should still run with appsettings defaults.
        }
    }

    private static void TryApplyLanguageResources(IServiceProvider services)
    {
        try
        {
            var triageOptions = services.GetService<IOptionsMonitor<TriageSettings>>();
            var language = triageOptions?.CurrentValue?.Language ?? "ko";

            var source = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                ? "Resources/Strings.en.xaml"
                : "Resources/Strings.ko.xaml";

            var merged = Current?.Resources.MergedDictionaries;
            if (merged is null)
            {
                return;
            }

            var existing = merged.FirstOrDefault(d =>
            {
                var s = d.Source?.OriginalString ?? string.Empty;
                return s.EndsWith("Resources/Strings.ko.xaml", StringComparison.OrdinalIgnoreCase) ||
                       s.EndsWith("Resources/Strings.en.xaml", StringComparison.OrdinalIgnoreCase);
            });

            var uri = new Uri(source, UriKind.Relative);
            if (existing is null)
            {
                merged.Insert(0, new ResourceDictionary { Source = uri });
            }
            else
            {
                existing.Source = uri;
            }
        }
        catch
        {
            // Ignore language selection issues; default resources should still work.
        }
    }
}
