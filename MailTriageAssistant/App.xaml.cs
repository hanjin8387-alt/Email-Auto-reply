using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using MailTriageAssistant.ViewModels;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MailTriageAssistant;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private readonly AppTrayManager _trayManager = new();
#if DEBUG
    private long? _startupMs;
    private double? _startupWorkingSetMb;
    private DispatcherTimer? _memorySnapshotTimer;
    private readonly List<MemorySnapshot> _memorySnapshots = new();

    private sealed record MemorySnapshot(
        DateTimeOffset utc,
        double working_set_mb,
        double managed_heap_mb,
        int gc_gen0,
        int gc_gen1,
        int gc_gen2);
#endif

    internal static bool IsExitRequested { get; private set; }
    internal static bool IsSystemTrayEnabled { get; private set; }
    internal static IServiceProvider? Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

#if DEBUG
        var perfAutoExit = e.Args.Any(static a => string.Equals(a, "--perf-auto-exit", StringComparison.OrdinalIgnoreCase));
#endif

        Window? splashWindow = null;
        try
        {
            splashWindow = CreateSplashWindow();
            splashWindow.Show();
        }
        catch
        {
            // Ignore splash screen failures.
        }

        var bootstrap = AppBootstrapper.Initialize();
        _serviceProvider = bootstrap.ServiceProvider;
        Services = _serviceProvider;

#if DEBUG
        var startupStart = Stopwatch.GetTimestamp();
#endif

        var appLogger = bootstrap.Logger;
        var mainWindow = bootstrap.MainWindow;
        MainWindow = mainWindow;

        TryInitializeSystemTray(mainWindow);

        mainWindow.Loaded += (_, _) =>
        {
            try
            {
                splashWindow?.Close();
            }
            catch
            {
                // Ignore splash close failures.
            }

            splashWindow = null;

#if DEBUG
            try
            {
                var elapsed = Stopwatch.GetElapsedTime(startupStart);
                var startupMs = (long)Math.Round(elapsed.TotalMilliseconds);
                var workingSetMb = Math.Round(Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d), 1);

                _startupMs = startupMs;
                _startupWorkingSetMb = workingSetMb;

                appLogger.LogInformation(
                    "Startup metrics: startup_ms={StartupMs}, memory_mb={MemoryMb}.",
                    startupMs,
                    workingSetMb);

                Helpers.PerfMetrics.AddTiming("startup_ms", startupMs);
            }
            catch
            {
                // Ignore startup measurement failures.
            }

            TryStartDebugMemorySnapshots(appLogger);

            if (perfAutoExit)
            {
                appLogger.LogInformation("Perf auto-exit enabled; shutting down after startup.");
                Dispatcher.BeginInvoke(() =>
                {
                    IsExitRequested = true;
                    Shutdown();
                }, DispatcherPriority.Background);
            }
#endif
        };

        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
#if DEBUG
        try
        {
            _memorySnapshotTimer?.Stop();
        }
        catch
        {
            // Ignore snapshot timer stop failures.
        }

        _memorySnapshotTimer = null;
        TryWritePerfMetrics();
#endif

        _trayManager.Dispose();
        IsSystemTrayEnabled = false;

        try
        {
            var stats = _serviceProvider?.GetService<SessionStatsService>();
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            if (stats is not null && logger is not null)
            {
                var snapshot = stats.Snapshot();
                logger.LogInformation(
                    "Session stats: HeadersLoaded={HeadersLoaded}, DigestsGenerated={DigestsGenerated}, DigestsCopied={DigestsCopied}, TeamsOpenAttempts={TeamsOpenAttempts}, Errors={Errors}, BodyRequested={BodyRequested}, BodyLoaded={BodyLoaded}, BodyFailed={BodyFailed}, BodyCanceled={BodyCanceled}, OutlookItemsSkipped={OutlookItemsSkipped}.",
                    snapshot.HeadersLoaded,
                    snapshot.DigestsGenerated,
                    snapshot.DigestsCopied,
                    snapshot.TeamsOpenAttempts,
                    snapshot.Errors,
                    snapshot.BodyBatchesRequested,
                    snapshot.BodyBatchesLoaded,
                    snapshot.BodyBatchesFailed,
                    snapshot.BodyBatchesCanceled,
                    snapshot.OutlookItemsSkipped);
            }
        }
        catch
        {
            // Ignore stats/logging failures on shutdown.
        }

        _serviceProvider?.Dispose();
        _serviceProvider = null;
        Services = null;

        Log.CloseAndFlush();

        base.OnExit(e);
    }

#if DEBUG
    private void TryWritePerfMetrics()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MailTriageAssistant");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, "perf_metrics.json");
            var exitWorkingSetMb = Math.Round(Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d), 1);

            var payload = new
            {
                generated_utc = DateTimeOffset.UtcNow,
                startup_ms = _startupMs,
                startup_working_set_mb = _startupWorkingSetMb,
                exit_working_set_mb = exitWorkingSetMb,
                memory_snapshots = _memorySnapshots,
                timings = Helpers.PerfMetrics.Snapshot(),
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore perf metrics failures on shutdown.
        }
    }

    private void TryStartDebugMemorySnapshots(ILogger<App> appLogger)
    {
        if (_memorySnapshotTimer is not null)
        {
            return;
        }

        try
        {
            var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMinutes(10),
            };

            timer.Tick += (_, _) => TryCaptureDebugMemorySnapshot(appLogger);
            _memorySnapshotTimer = timer;

            TryCaptureDebugMemorySnapshot(appLogger);
            timer.Start();
        }
        catch
        {
            // Ignore snapshot timer startup failures.
        }
    }

    private void TryCaptureDebugMemorySnapshot(ILogger<App> appLogger)
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            var workingSetMb = Math.Round(proc.WorkingSet64 / (1024d * 1024d), 1);
            var managedHeapMb = Math.Round(GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d), 1);

            var snapshot = new MemorySnapshot(
                utc: DateTimeOffset.UtcNow,
                working_set_mb: workingSetMb,
                managed_heap_mb: managedHeapMb,
                gc_gen0: GC.CollectionCount(0),
                gc_gen1: GC.CollectionCount(1),
                gc_gen2: GC.CollectionCount(2));

            _memorySnapshots.Add(snapshot);

            appLogger.LogInformation(
                "Memory snapshot: working_set_mb={WorkingSetMb}, managed_heap_mb={ManagedHeapMb}, gc_gen0={Gen0}, gc_gen1={Gen1}, gc_gen2={Gen2}.",
                workingSetMb,
                managedHeapMb,
                snapshot.gc_gen0,
                snapshot.gc_gen1,
                snapshot.gc_gen2);
        }
        catch
        {
            // Ignore memory snapshot failures.
        }
    }
#endif

    private void TryInitializeSystemTray(MainWindow mainWindow)
    {
        try
        {
            var triageOptions = _serviceProvider?.GetService<IOptionsMonitor<TriageSettings>>();
            var enabled = triageOptions?.CurrentValue?.EnableSystemTray ?? true;
            if (mainWindow.DataContext is not MainViewModel vm)
            {
                return;
            }

            IsSystemTrayEnabled = _trayManager.TryInitialize(
                mainWindow,
                vm,
                enabled,
                () =>
                {
                    IsExitRequested = true;
                    Shutdown();
                });
        }
        catch
        {
            // Ignore system tray initialization failures; app should still run.
        }
    }

    private static Window CreateSplashWindow()
    {
        var uri = new Uri("pack://application:,,,/Resources/Splash.png", UriKind.Absolute);

        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = uri;
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        var image = new System.Windows.Controls.Image
        {
            Source = bitmap,
            Stretch = System.Windows.Media.Stretch.UniformToFill,
        };

        return new Window
        {
            Title = AppLocalizationManager.GetResourceString("Str.MainWindow.Title"),
            Width = 640,
            Height = 360,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = System.Windows.Media.Brushes.White,
            Content = image,
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => AppExceptionPolicy.HandleDispatcherUnhandledException(_serviceProvider, e);
}
