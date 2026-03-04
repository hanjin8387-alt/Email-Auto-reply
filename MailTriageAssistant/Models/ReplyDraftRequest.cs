namespace MailTriageAssistant.Models;

public sealed record ReplyDraftRequest(
    string To,
    string Subject,
    string Body);
