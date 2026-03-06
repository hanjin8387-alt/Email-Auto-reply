using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class CreateReplyDraftWorkflow
{
    private readonly ITemplateService _templateService;
    private readonly IOutlookMailGateway _mailGateway;
    private readonly IClock _clock;

    public CreateReplyDraftWorkflow(
        ITemplateService templateService,
        IOutlookMailGateway mailGateway,
        IClock clock)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _mailGateway = mailGateway ?? throw new ArgumentNullException(nameof(mailGateway));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ReplyDraftValidationResult Validate(
        AnalyzedItem email,
        ReplyTemplate template,
        IReadOnlyList<ReplyTemplateFieldInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        if (string.IsNullOrWhiteSpace(email.ReplyToAddress))
        {
            return new ReplyDraftValidationResult(
                IsValid: false,
                MissingRequiredFields: Array.Empty<string>(),
                MissingSenderAddress: true,
                HasUnresolvedPlaceholders: false);
        }

        var values = BuildTemplateValues(template, inputs, email);
        var missingRequired = GetMissingRequiredFields(template, values).ToArray();
        if (missingRequired.Length > 0)
        {
            return new ReplyDraftValidationResult(
                IsValid: false,
                MissingRequiredFields: missingRequired,
                MissingSenderAddress: false,
                HasUnresolvedPlaceholders: false);
        }

        var body = _templateService.FillTemplate(template.BodyContent ?? string.Empty, values);
        var unresolved = _templateService.ExtractPlaceholders(body).Count > 0;

        return new ReplyDraftValidationResult(
            IsValid: !unresolved,
            MissingRequiredFields: Array.Empty<string>(),
            MissingSenderAddress: false,
            HasUnresolvedPlaceholders: unresolved);
    }

    public async Task<string> CreateDraftAsync(
        AnalyzedItem email,
        ReplyTemplate template,
        IReadOnlyList<ReplyTemplateFieldInput> inputs,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        var values = BuildTemplateValues(template, inputs, email);
        var body = _templateService.FillTemplate(template.BodyContent ?? string.Empty, values);
        var subject = BuildReplySubject(email.RawContent.Subject);

        var request = new ReplyDraftRequest(
            To: email.ReplyToAddress,
            Subject: subject,
            Body: body);

        await _mailGateway.CreateDraftAsync(request, ct).ConfigureAwait(false);
        return LocalizedStrings.Get("Str.Status.ReplyDraftCreated", "Outlook draft created.");
    }

    public IReadOnlyList<ReplyTemplateFieldInput> BuildTemplateFieldInputs(ReplyTemplate template, AnalyzedItem? selectedEmail)
    {
        ArgumentNullException.ThrowIfNull(template);

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inputs = new List<ReplyTemplateFieldInput>();
        foreach (var field in template.Fields ?? Array.Empty<ReplyTemplateField>())
        {
            var key = field.Key;
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
            {
                continue;
            }

            inputs.Add(
                new ReplyTemplateFieldInput(
                    key: key,
                    label: field.Label,
                    isRequired: field.IsRequired,
                    placeholder: field.Placeholder,
                    value: GetTemplateDefaultValue(field, selectedEmail)));
        }

        return inputs;
    }

    private Dictionary<string, string> BuildTemplateValues(
        ReplyTemplate template,
        IReadOnlyList<ReplyTemplateFieldInput> inputs,
        AnalyzedItem? selectedEmail)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fieldsByKey = (template.Fields ?? Array.Empty<ReplyTemplateField>())
            .Where(static field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(static field => field.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var key in _templateService.ExtractPlaceholders(template.BodyContent ?? string.Empty))
        {
            if (string.IsNullOrWhiteSpace(key) || values.ContainsKey(key))
            {
                continue;
            }

            values[key] = fieldsByKey.TryGetValue(key, out var field)
                ? GetTemplateDefaultValue(field, selectedEmail)
                : string.Empty;
        }

        foreach (var input in inputs)
        {
            values[input.Key] = input.Value ?? string.Empty;
        }

        return values;
    }

    private IEnumerable<string> GetMissingRequiredFields(
        ReplyTemplate template,
        IReadOnlyDictionary<string, string> values)
    {
        foreach (var field in template.Fields ?? Array.Empty<ReplyTemplateField>())
        {
            if (!field.IsRequired)
            {
                continue;
            }

            var key = field.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                yield return string.IsNullOrWhiteSpace(field.Label) ? key : field.Label;
            }
        }
    }

    private static string BuildReplySubject(string subject)
    {
        var normalized = subject ?? string.Empty;
        var prefix = LocalizedStrings.Get("Str.Mail.RePrefix", "RE:");
        if (!normalized.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{prefix} {normalized}";
        }

        return normalized;
    }

    private string GetTemplateDefaultValue(ReplyTemplateField field, AnalyzedItem? selectedEmail)
    {
        ArgumentNullException.ThrowIfNull(field);

        var defaultValue = field.DefaultValue ?? new ReplyTemplateFieldDefaultValue();
        return defaultValue.Kind switch
        {
            ReplyTemplateFieldDefaultKind.Literal => defaultValue.Value ?? string.Empty,
            ReplyTemplateFieldDefaultKind.SelectedEmailSubject => selectedEmail?.RedactedContent.Subject ?? string.Empty,
            ReplyTemplateFieldDefaultKind.CurrentDateOffset => _clock.Now.AddDays(defaultValue.OffsetDays).ToString(
                string.IsNullOrWhiteSpace(defaultValue.DateFormat) ? "yyyy-MM-dd" : defaultValue.DateFormat),
            _ => string.Empty,
        };
    }
}

public sealed record ReplyDraftValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingRequiredFields,
    bool MissingSenderAddress,
    bool HasUnresolvedPlaceholders);
