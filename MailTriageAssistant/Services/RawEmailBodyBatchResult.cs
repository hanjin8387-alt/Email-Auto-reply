using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed record RawEmailBodyBatchResult(
    IReadOnlyDictionary<string, RawEmailContent> LoadedByEntryId,
    int RequestedCount,
    int LoadedCount,
    int FailedCount,
    int CanceledCount);
