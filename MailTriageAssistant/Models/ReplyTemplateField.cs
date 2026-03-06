namespace MailTriageAssistant.Models;

public sealed class ReplyTemplateField
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsRequired { get; init; } = true;
    public string Placeholder { get; init; } = string.Empty;
    public ReplyTemplateFieldDefaultValue DefaultValue { get; init; } = new();
}
