namespace MailTriageAssistant.Services;

public enum InboxRefreshOutcome
{
    Success,
    Failure,
    Cancelled,
    NotSupported,
    Unavailable,
}
