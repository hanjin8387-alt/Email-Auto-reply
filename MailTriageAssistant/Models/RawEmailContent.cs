namespace MailTriageAssistant.Models;

public sealed record RawEmailContent(
    string SenderName,
    string SenderEmail,
    string Subject,
    string Body)
{
    public static readonly RawEmailContent Empty = new(
        SenderName: string.Empty,
        SenderEmail: string.Empty,
        Subject: string.Empty,
        Body: string.Empty);
}
