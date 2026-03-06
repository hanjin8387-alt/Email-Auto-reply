namespace MailTriageAssistant.Models;

public sealed class ReplyTemplateFieldDefaultValue
{
    public ReplyTemplateFieldDefaultKind Kind { get; init; } = ReplyTemplateFieldDefaultKind.None;

    public string Value { get; init; } = string.Empty;

    public int OffsetDays { get; init; }

    public string DateFormat { get; init; } = "yyyy-MM-dd";
}
