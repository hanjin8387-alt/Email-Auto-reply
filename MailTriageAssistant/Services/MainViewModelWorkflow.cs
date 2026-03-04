using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class MainViewModelWorkflow : IMainViewModelWorkflow
{
    private readonly InboxRefreshCoordinator _refreshCoordinator;
    private readonly SelectedEmailBodyLoader _selectedBodyLoader;
    private readonly GenerateDigestWorkflow _generateDigestWorkflow;
    private readonly CreateReplyDraftWorkflow _createReplyDraftWorkflow;
    private readonly IOutlookMailGateway _outlookMailGateway;
    private readonly IDialogService _dialogService;
    private readonly SessionStatsService _sessionStats;
    private readonly ILogger<MainViewModelWorkflow> _logger;

    public MainViewModelWorkflow(
        InboxRefreshCoordinator refreshCoordinator,
        SelectedEmailBodyLoader selectedBodyLoader,
        GenerateDigestWorkflow generateDigestWorkflow,
        CreateReplyDraftWorkflow createReplyDraftWorkflow,
        IOutlookMailGateway outlookMailGateway,
        IDialogService dialogService,
        SessionStatsService sessionStats,
        ILogger<MainViewModelWorkflow> logger)
    {
        _refreshCoordinator = refreshCoordinator ?? throw new ArgumentNullException(nameof(refreshCoordinator));
        _selectedBodyLoader = selectedBodyLoader ?? throw new ArgumentNullException(nameof(selectedBodyLoader));
        _generateDigestWorkflow = generateDigestWorkflow ?? throw new ArgumentNullException(nameof(generateDigestWorkflow));
        _createReplyDraftWorkflow = createReplyDraftWorkflow ?? throw new ArgumentNullException(nameof(createReplyDraftWorkflow));
        _outlookMailGateway = outlookMailGateway ?? throw new ArgumentNullException(nameof(outlookMailGateway));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _sessionStats = sessionStats ?? throw new ArgumentNullException(nameof(sessionStats));
        _logger = logger ?? NullLogger<MainViewModelWorkflow>.Instance;
    }

    public async Task<LoadEmailsWorkflowResult> LoadEmailsAsync(
        IReadOnlyList<AnalyzedItem> existingItems,
        string? selectedEntryId,
        bool showDialogs,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingItems);

        try
        {
            var result = await _refreshCoordinator
                .RefreshAsync(existingItems, ct)
                .ConfigureAwait(false);

            var restoredSelectionEntryId = result.Outcome == InboxRefreshOutcome.Success
                ? EmailListProjectionService.RestoreSelection(result.SortedItems, selectedEntryId)?.EntryId
                : selectedEntryId;

            return new LoadEmailsWorkflowResult(
                result.Outcome,
                result.SortedItems,
                result.StatusMessage,
                restoredSelectionEntryId,
                result.PrefetchTask);
        }
        catch (OperationCanceledException)
        {
            return new LoadEmailsWorkflowResult(
                InboxRefreshOutcome.Cancelled,
                existingItems.ToArray(),
                LocalizedStrings.Get("Str.Status.LoadCanceled", "Email load canceled."),
                selectedEntryId,
                Task.CompletedTask);
        }
        catch (NotSupportedException)
        {
            _sessionStats.RecordError();
            var message = HandleOutlookNotSupported(showDialogs);
            return new LoadEmailsWorkflowResult(
                InboxRefreshOutcome.Failure,
                existingItems.ToArray(),
                message,
                selectedEntryId,
                Task.CompletedTask);
        }
        catch (InvalidOperationException)
        {
            _sessionStats.RecordError();
            var message = HandleOutlookUnavailable(showDialogs);
            return new LoadEmailsWorkflowResult(
                InboxRefreshOutcome.Failure,
                existingItems.ToArray(),
                message,
                selectedEntryId,
                Task.CompletedTask);
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("LoadEmails failed: {ExceptionType}.", ex.GetType().Name);

            var message = LocalizedStrings.Get("Str.Status.LoadFailed", "Failed to load emails.");
            if (showDialogs)
            {
                _dialogService.ShowError(
                    message,
                    LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
            }

            return new LoadEmailsWorkflowResult(
                InboxRefreshOutcome.Failure,
                existingItems.ToArray(),
                message,
                selectedEntryId,
                Task.CompletedTask);
        }
    }

    public async Task<SelectedBodyLoadWorkflowResult> LoadSelectedBodyAsync(AnalyzedItem? item, CancellationToken ct = default)
    {
        if (item is null || item.IsBodyLoaded)
        {
            return new SelectedBodyLoadWorkflowResult(Loaded: false, StatusMessage: null);
        }

        try
        {
            var loaded = await _selectedBodyLoader.LoadSelectedBodyAsync(item, ct).ConfigureAwait(false);
            if (!loaded)
            {
                return new SelectedBodyLoadWorkflowResult(Loaded: false, StatusMessage: null);
            }

            return new SelectedBodyLoadWorkflowResult(
                Loaded: true,
                StatusMessage: LocalizedStrings.Get("Str.Status.BodyLoaded", "Body loaded."));
        }
        catch (NotSupportedException)
        {
            return new SelectedBodyLoadWorkflowResult(
                Loaded: false,
                StatusMessage: HandleOutlookNotSupported(showDialog: true));
        }
        catch (InvalidOperationException)
        {
            return new SelectedBodyLoadWorkflowResult(
                Loaded: false,
                StatusMessage: HandleOutlookUnavailable(showDialog: true));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("Body load failed: {ExceptionType}.", ex.GetType().Name);

            var message = LocalizedStrings.Get("Str.Status.BodyLoadFailed", "Failed to load body.");
            _dialogService.ShowError(
                message,
                LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
            return new SelectedBodyLoadWorkflowResult(Loaded: false, StatusMessage: message);
        }
    }

    public Task<CommandWorkflowResult> GenerateDigestAsync(
        IReadOnlyList<AnalyzedItem> emails,
        Task prefetchTask,
        string? teamsUserEmail,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emails);
        ArgumentNullException.ThrowIfNull(prefetchTask);

        return ExecuteOutlookOperationAsync(
            async token =>
            {
                var result = await _generateDigestWorkflow
                    .RunAsync(emails, prefetchTask, teamsUserEmail, token)
                    .ConfigureAwait(false);

                if (result.TeamsOpened)
                {
                    _dialogService.ShowInfo(
                        LocalizedStrings.Get(
                            "Str.Dialog.DigestTeamsOpenedMessage",
                            "Digest copied to clipboard and Teams opening."),
                        LocalizedStrings.Get("Str.Dialog.DigestReadyTitle", "Digest Ready"));
                }
                else
                {
                    _dialogService.ShowInfo(
                        LocalizedStrings.Get(
                            "Str.Dialog.DigestTeamsFailedMessage",
                            "Digest copied to clipboard. Open Teams manually."),
                        LocalizedStrings.Get("Str.Dialog.DigestReadyTitle", "Digest Ready"));
                }

                return result.StatusMessage;
            },
            LocalizedStrings.Get("Str.Status.DigestFailed", "Digest generation failed."),
            ct);
    }

    public Task<CommandWorkflowResult> ReplyAsync(
        AnalyzedItem? email,
        ReplyTemplate? template,
        IReadOnlyList<ReplyTemplateFieldInput> inputs,
        CancellationToken ct = default)
    {
        if (email is null || template is null)
        {
            return Task.FromResult(new CommandWorkflowResult(Performed: false, StatusMessage: null));
        }

        ArgumentNullException.ThrowIfNull(inputs);

        var validation = _createReplyDraftWorkflow.Validate(email, template, inputs);
        if (validation.MissingSenderAddress)
        {
            _dialogService.ShowInfo(
                LocalizedStrings.Get(
                    "Str.Dialog.ReplyMissingSenderMessage",
                    "Sender email address is not available."),
                LocalizedStrings.Get("Str.Dialog.TemplateReplyTitle", "Template Reply"));
            return Task.FromResult(new CommandWorkflowResult(Performed: false, StatusMessage: null));
        }

        if (validation.MissingRequiredFields.Count > 0)
        {
            var warningPrefix = LocalizedStrings.Get(
                "Str.Dialog.TemplateMissingInputMessage",
                "Required template fields are missing.");
            var fields = string.Join(", ", validation.MissingRequiredFields);
            _dialogService.ShowWarning(
                $"{warningPrefix}\n{fields}",
                LocalizedStrings.Get("Str.Dialog.TemplateReplyTitle", "Template Reply"));
            return Task.FromResult(new CommandWorkflowResult(Performed: false, StatusMessage: null));
        }

        if (validation.HasUnresolvedPlaceholders)
        {
            _dialogService.ShowWarning(
                LocalizedStrings.Get(
                    "Str.Dialog.TemplateUnresolvedPlaceholderMessage",
                    "Template contains unresolved placeholders."),
                LocalizedStrings.Get("Str.Dialog.TemplateReplyTitle", "Template Reply"));
            return Task.FromResult(new CommandWorkflowResult(Performed: false, StatusMessage: null));
        }

        return ExecuteOutlookOperationAsync(
            async token =>
            {
                return await _createReplyDraftWorkflow
                    .CreateDraftAsync(email, template, inputs, token)
                    .ConfigureAwait(false);
            },
            LocalizedStrings.Get("Str.Status.ReplyDraftFailed", "Failed to create reply draft."),
            ct);
    }

    public Task<CommandWorkflowResult> OpenInOutlookAsync(AnalyzedItem? email, CancellationToken ct = default)
    {
        if (email is null)
        {
            return Task.FromResult(new CommandWorkflowResult(Performed: false, StatusMessage: null));
        }

        return ExecuteOutlookOperationAsync(
            async token =>
            {
                await _outlookMailGateway.OpenItemAsync(email.EntryId, token).ConfigureAwait(false);
                return LocalizedStrings.Get("Str.Status.OpenedInOutlook", "Opened in Outlook.");
            },
            LocalizedStrings.Get("Str.Status.OpenInOutlookFailed", "Failed to open in Outlook."),
            ct);
    }

    private async Task<CommandWorkflowResult> ExecuteOutlookOperationAsync(
        Func<CancellationToken, Task<string>> operation,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            var status = await operation(ct).ConfigureAwait(false);
            return new CommandWorkflowResult(Performed: true, StatusMessage: status);
        }
        catch (NotSupportedException)
        {
            return new CommandWorkflowResult(
                Performed: true,
                StatusMessage: HandleOutlookNotSupported(showDialog: true));
        }
        catch (InvalidOperationException)
        {
            return new CommandWorkflowResult(
                Performed: true,
                StatusMessage: HandleOutlookUnavailable(showDialog: true));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("{ErrorMessage}: {ExceptionType}.", errorMessage, ex.GetType().Name);
            _dialogService.ShowError(
                errorMessage,
                LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
            return new CommandWorkflowResult(Performed: true, StatusMessage: errorMessage);
        }
    }

    private string HandleOutlookNotSupported(bool showDialog)
    {
        _sessionStats.RecordError();
        var message = LocalizedStrings.Get(
            "Str.Dialog.OutlookNotSupportedMessage",
            "Classic Outlook is required. New Outlook is not supported.");
        if (showDialog)
        {
            _dialogService.ShowWarning(
                message,
                LocalizedStrings.Get("Str.Dialog.OutlookTitle", "Outlook"));
        }

        return message;
    }

    private string HandleOutlookUnavailable(bool showDialog)
    {
        _sessionStats.RecordError();
        var message = LocalizedStrings.Get(
            "Str.Dialog.OutlookUnavailableMessage",
            "Outlook is unavailable. Start Classic Outlook and retry.");
        if (showDialog)
        {
            _dialogService.ShowInfo(
                message,
                LocalizedStrings.Get("Str.Dialog.OutlookTitle", "Outlook"));
        }

        return message;
    }
}
