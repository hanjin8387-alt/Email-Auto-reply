namespace MailTriageAssistant.Services;

public sealed record OutlookCapabilitySnapshot(
    bool HasClassicOutlook,
    bool HasNewOutlook,
    bool IsNewOutlookOnly,
    string DiagnosticCode);
