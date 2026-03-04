namespace MailTriageAssistant.Services;

public sealed record BodyLoadApplyResult(
    int RequestedCount,
    int LoadedCount,
    int FailedCount,
    int CanceledCount,
    int MissingCount)
{
    public bool HasPartialFailures => FailedCount > 0 || CanceledCount > 0 || MissingCount > 0;
}
