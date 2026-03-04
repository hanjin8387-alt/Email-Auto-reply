using System;

namespace MailTriageAssistant.Models;

public sealed record DigestEmailItem(
    int Score,
    DateTime ReceivedTime,
    string RedactedSender,
    string RedactedSubject,
    string RedactedSummary);
