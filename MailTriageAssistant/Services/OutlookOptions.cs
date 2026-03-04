namespace MailTriageAssistant.Services;

public sealed class OutlookOptions
{
    public int ComTimeoutSeconds { get; set; } = 30;

    public int HeadersCacheTtlSeconds { get; set; } = 30;

    public int MaxFetchCount { get; set; } = 50;

    public int MaxBodyLength { get; set; } = 1500;

    public int RestrictDays { get; set; } = 7;

    public int CapabilityCheckTtlMinutes { get; set; } = 5;
}
