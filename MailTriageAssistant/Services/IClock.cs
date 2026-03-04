using System;

namespace MailTriageAssistant.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
    DateTimeOffset UtcNow { get; }
}
