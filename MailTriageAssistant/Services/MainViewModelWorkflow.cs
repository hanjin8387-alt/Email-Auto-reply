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
                InboxRefreshOutcome.NotSupported,
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
                InboxRefreshOutcome.Unavailable,
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
            return new SelectedBodyLoadWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null);
        }

        try
        {
            var loaded = await _selectedBodyLoader.LoadSelectedBodyAsync(item, ct).ConfigureAwait(false);
            if (!loaded)
            {
                return new SelectedBodyLoadWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null);
            }

            return new SelectedBodyLoadWorkflowResult(
                WorkflowActionOutcome.Success,
                StatusMessage: LocalizedStrings.Get("Str.Status.BodyLoaded", "Body loaded."));
        }
        catch (NotSupportedException)
        {
            return new SelectedBodyLoadWorkflowResult(
                WorkflowActionOutcome.NotSupported,
                StatusMessage: HandleOutlookNotSupported(showDialog: true));
        }
        catch (InvalidOperationException)
        {
            return new SelectedBodyLoadWorkflowResult(
                WorkflowActionOutcome.Unavailable,
                StatusMessage: HandleOutlookUnavailable(showDialog: true));
        }
        catch (OperationCanceledException)
        {
            return new SelectedBodyLoadWorkflowResult(
                WorkflowActionOutcome.Cancelled,
                StatusMessage: LocalizedStrings.Get("Str.Status.BodyLoadCanceled", "Body load canceled."));
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("Body load failed: {ExceptionType}.", ex.GetType().Name);

            var message = LocalizedStrings.Get("Str.Status.BodyLoadFailed", "Failed to load body.");
            _dialogService.ShowError(
                message,
                LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
            return new SelectedBodyLoadWorkflowResult(WorkflowActionOutcome.Failure, StatusMessage: message);
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

                if (result.Outcome == WorkflowActionOutcome.Success && result.TeamsOpened)
                {
                    _dialogService.ShowInfo(
                        LocalizedStrings.Get(
                            "Str.Dialog.DigestTeamsOpenedMessage",
                            "Digest copied to clipboard and Teams opening."),
                        LocalizedStrings.Get("Str.Dialog.DigestReadyTitle", "Digest Ready"));
                }
                else if (result.Outcome == WorkflowActionOutcome.Success)
                {
                    _dialogService.ShowInfo(
                        LocalizedStrings.Get(
                            "Str.Dialog.DigestTeamsFailedMessage",
                            "Digest copied to clipboard. Open Teams manually."),
                        LocalizedStrings.Get("Str.Dialog.DigestReadyTitle", "Digest Ready"));
                }

                return new CommandWorkflowResult(result.Outcome, result.StatusMessage);
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
            return Task.FromResult(new CommandWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null));
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
            return Task.FromResult(new CommandWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null));
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
            return Task.FromResult(new CommandWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null));
        }

        if (validation.HasUnresolvedPlaceholders)
        {
            _dialogService.ShowWarning(
                LocalizedStrings.Get(
                    "Str.Dialog.TemplateUnresolvedPlaceholderMessage",
                    "Template contains unresolved placeholders."),
                LocalizedStrings.Get("Str.Dialog.TemplateReplyTitle", "Template Reply"));
            return Task.FromResult(new CommandWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null));
        }

        return ExecuteOutlookOperationAsync(
            async token =>
            {
                var status = await _createReplyDraftWorkflow
                    .CreateDraftAsync(email, template, inputs, token)
                    .ConfigureAwait(false);
                return new CommandWorkflowResult(WorkflowActionOutcome.Success, status);
            },
            LocalizedStrings.Get("Str.Status.ReplyDraftFailed", "Failed to create reply draft."),
            ct);
    }

    public Task<CommandWorkflowResult> OpenInOutlookAsync(AnalyzedItem? email, CancellationToken ct = default)
    {
        if (email is null)
        {
            return Task.FromResult(new CommandWorkflowResult(WorkflowActionOutcome.Skipped, StatusMessage: null));
        }

        return ExecuteOutlookOperationAsync(
            async token =>
            {
                await _outlookMailGateway.OpenItemAsync(email.EntryId, token).ConfigureAwait(false);
                return new CommandWorkflowResult(
                    WorkflowActionOutcome.Success,
                    LocalizedStrings.Get("Str.Status.OpenedInOutlook", "Opened in Outlook."));
            },
            LocalizedStrings.Get("Str.Status.OpenInOutlookFailed", "Failed to open in Outlook."),
            ct);
    }

    private async Task<CommandWorkflowResult> ExecuteOutlookOperationAsync(
        Func<CancellationToken, Task<CommandWorkflowResult>> operation,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            return new CommandWorkflowResult(
                WorkflowActionOutcome.NotSupported,
                StatusMessage: HandleOutlookNotSupported(showDialog: true));
        }
        catch (InvalidOperationException)
        {
            return new CommandWorkflowResult(
                WorkflowActionOutcome.Unavailable,
                StatusMessage: HandleOutlookUnavailable(showDialog: true));
        }
        catch (OperationCanceledException)
        {
            return new CommandWorkflowResult(
                WorkflowActionOutcome.Cancelled,
                StatusMessage: LocalizedStrings.Get("Str.Status.OperationCanceled", "Operation canceled."));
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("{ErrorMessage}: {ExceptionType}.", errorMessage, ex.GetType().Name);
            _dialogService.ShowError(
                errorMessage,
                LocalizedStrings.Get("Str.Dialog.ErrorTitle", "Error"));
            return new CommandWorkflowResult(WorkflowActionOutcome.Failure, StatusMessage: errorMessage);
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
