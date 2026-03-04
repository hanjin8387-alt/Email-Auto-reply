namespace MailTriageAssistant.Models;

public sealed record RedactedEmailContent(
    string Sender,
    string Subject,
    string Summary)
{
    public static readonly RedactedEmailContent Empty = new(
        Sender: string.Empty,
        Subject: string.Empty,
        Summary: string.Empty);
}
