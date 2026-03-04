using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class AutoRefreshControllerFactory : IAutoRefreshControllerFactory
{
    private readonly IClock _clock;
    private readonly IOptionsMonitor<TriageSettings> _settingsMonitor;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AutoRefreshController> _logger;

    public AutoRefreshControllerFactory(
        IClock clock,
        IOptionsMonitor<TriageSettings> settingsMonitor,
        IDialogService dialogService,
        ILogger<AutoRefreshController> logger)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAutoRefreshController Create(
        Func<CancellationToken, Task<InboxRefreshOutcome>> refreshOperation,
        Func<bool> isLoading,
        Action<string> setStatusMessage)
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        return new AutoRefreshController(
            dispatcher: dispatcher,
            clock: _clock,
            settingsMonitor: _settingsMonitor,
            refreshOperation: refreshOperation,
            isLoading: isLoading,
            setStatusMessage: setStatusMessage,
            dialogService: _dialogService,
            logger: _logger);
    }
}
