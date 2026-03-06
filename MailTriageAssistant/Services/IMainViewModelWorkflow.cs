using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IMainViewModelWorkflow
{
    Task<LoadEmailsWorkflowResult> LoadEmailsAsync(
        IReadOnlyList<AnalyzedItem> existingItems,
        string? selectedEntryId,
        bool showDialogs,
        CancellationToken ct = default);

    Task<SelectedBodyLoadWorkflowResult> LoadSelectedBodyAsync(AnalyzedItem? item, CancellationToken ct = default);

    Task<CommandWorkflowResult> GenerateDigestAsync(
        IReadOnlyList<AnalyzedItem> emails,
        Task prefetchTask,
        string? teamsUserEmail,
        CancellationToken ct = default);

    Task<CommandWorkflowResult> ReplyAsync(
        AnalyzedItem? email,
        ReplyTemplate? template,
        IReadOnlyList<ReplyTemplateFieldInput> inputs,
        CancellationToken ct = default);

    Task<CommandWorkflowResult> OpenInOutlookAsync(AnalyzedItem? email, CancellationToken ct = default);
}

public enum WorkflowActionOutcome
{
    Skipped,
    Success,
    Cancelled,
    NotSupported,
    Unavailable,
    Failure,
}

public sealed record LoadEmailsWorkflowResult(
    InboxRefreshOutcome Outcome,
    IReadOnlyList<AnalyzedItem> SortedItems,
    string StatusMessage,
    string? RestoredSelectionEntryId,
    Task PrefetchTask);

public sealed record SelectedBodyLoadWorkflowResult(WorkflowActionOutcome Outcome, string? StatusMessage)
{
    public bool Loaded => Outcome == WorkflowActionOutcome.Success;
}

public sealed record CommandWorkflowResult(WorkflowActionOutcome Outcome, string? StatusMessage);
