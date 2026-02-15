using System;

namespace MailTriageAssistant.Models;

public sealed class RawEmailHeader
{
    public string EntryId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string SenderEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime ReceivedTime { get; init; }
    public bool HasAttachments { get; init; }
}
