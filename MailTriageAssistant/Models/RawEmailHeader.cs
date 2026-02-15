using System;

namespace MailTriageAssistant.Models;

public sealed class RawEmailHeader
{
    public string EntryId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedTime { get; set; }
    public bool HasAttachments { get; set; }
}

