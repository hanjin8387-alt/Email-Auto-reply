using System.Collections.Generic;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed record InboxRefreshResult(
    InboxRefreshOutcome Outcome,
    IReadOnlyList<AnalyzedItem> SortedItems,
    string StatusMessage,
    Task PrefetchTask);
