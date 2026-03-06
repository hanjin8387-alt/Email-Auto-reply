using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using MailTriageAssistant.ViewModels;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Text.Json;

namespace MailTriageAssistant;

public static class AppServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        var configuration = CreateConfiguration();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<TriageSettings>(configuration.GetSection(nameof(TriageSettings)));
        services.Configure<OutlookOptions>(configuration.GetSection(nameof(OutlookOptions)));
        services.Configure<TemplateCatalogOptions>(configuration.GetSection(nameof(TemplateCatalogOptions)));
        services.Configure<DigestPromptOptions>(configuration.GetSection(nameof(DigestPromptOptions)));
        services.AddOptions<TemplateCatalogOptions>()
            .PostConfigure<IOptionsMonitor<TriageSettings>>((options, triageMonitor) =>
            {
                options.ReplyTemplatesPath = AppLocalizationManager.ResolveLocalizedContentPath(
                    configuredPath: options.ReplyTemplatesPath,
                    language: triageMonitor.CurrentValue.Language,
                    filePrefix: "Resources/Templates/reply_templates",
                    extensionWithoutDot: "json");
            });
        services.AddOptions<DigestPromptOptions>()
            .PostConfigure<IOptionsMonitor<TriageSettings>>((options, triageMonitor) =>
            {
                options.PromptPath = AppLocalizationManager.ResolveLocalizedContentPath(
                    configuredPath: options.PromptPath,
                    language: triageMonitor.CurrentValue.Language,
                    filePrefix: "Resources/Prompts/digest_prompt",
                    extensionWithoutDot: "md");
            });

        ConfigureLogging(services);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IClipboardService, WpfClipboardService>();
        services.AddSingleton<IExternalLauncher, ShellExternalLauncher>();
        services.AddSingleton<SessionStatsService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<VipSenderProvider>();
        services.AddSingleton<RedactionService>();
        services.AddSingleton<IRedactionService>(sp => sp.GetRequiredService<RedactionService>());
        services.AddSingleton<ClipboardSecurityHelper>();
        services.AddSingleton<IOutlookCapabilityDetector, OutlookCapabilityDetector>();
        services.AddSingleton<IOutlookSessionHost, OutlookSessionHost>();
        services.AddSingleton<IOutlookInboxReader, OutlookInboxReader>();
        services.AddSingleton<IOutlookBodyReader, OutlookBodyReader>();
        services.AddSingleton<IOutlookDraftComposer, OutlookDraftComposer>();
        services.AddSingleton<IOutlookItemLauncher, OutlookItemLauncher>();
        services.AddSingleton<OutlookService>();
        services.AddSingleton<IOutlookMailGateway>(sp => sp.GetRequiredService<OutlookService>());
        services.AddSingleton<TriageService>();
        services.AddSingleton<ITriageService>(sp => sp.GetRequiredService<TriageService>());
        services.AddSingleton<IDigestPromptProvider, FileDigestPromptProvider>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<IDigestService>(sp => sp.GetRequiredService<DigestService>());
        services.AddSingleton<IDigestDeliveryService, DigestDeliveryService>();
        services.AddSingleton<ITemplateCatalogLoader, JsonTemplateCatalogLoader>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddSingleton<ITemplatePlaceholderValidator, TemplatePlaceholderValidator>();
        services.AddSingleton<TemplateService>();
        services.AddSingleton<ITemplateService>(sp => sp.GetRequiredService<TemplateService>());
        services.AddSingleton<EmailListProjectionService>();
        services.AddSingleton<SelectedEmailBodyLoader>();
        services.AddSingleton<InboxRefreshCoordinator>();
        services.AddSingleton<GenerateDigestWorkflow>();
        services.AddSingleton<CreateReplyDraftWorkflow>();
        services.AddSingleton<IMainViewModelWorkflow, MainViewModelWorkflow>();
        services.AddSingleton<IAutoRefreshControllerFactory, AutoRefreshControllerFactory>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    private static IConfiguration CreateConfiguration()
    {
        try
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();
        }
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
}
