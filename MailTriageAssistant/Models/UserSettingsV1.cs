using System;

namespace MailTriageAssistant.Models;

public sealed class UserSettingsV1
{
    public const int Version = 1;

    public int SchemaVersion { get; init; } = Version;

    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string[] VipSenders { get; init; } = Array.Empty<string>();
}
