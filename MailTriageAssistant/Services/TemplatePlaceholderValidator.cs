using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class TemplatePlaceholderValidator : ITemplatePlaceholderValidator
{
    private readonly ITemplateRenderer _renderer;

    public TemplatePlaceholderValidator(ITemplateRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public void ValidateTemplates(IReadOnlyList<ReplyTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var duplicateIds = templates
            .GroupBy(static x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateIds is not null)
        {
            throw new InvalidDataException($"Duplicate template id detected: {duplicateIds.Key}");
        }

        foreach (var template in templates)
        {
            ValidateTemplate(template);
        }
    }

    private void ValidateTemplate(ReplyTemplate template)
    {
        var placeholders = _renderer.ExtractPlaceholders(template.BodyContent ?? string.Empty);
        var placeholderSet = new HashSet<string>(placeholders, StringComparer.OrdinalIgnoreCase);
        var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in template.Fields ?? Array.Empty<ReplyTemplateField>())
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                throw new InvalidDataException($"Template '{template.Id}' contains empty field key.");
            }

            if (!fieldKeys.Add(field.Key))
            {
                throw new InvalidDataException($"Template '{template.Id}' contains duplicated field key '{field.Key}'.");
            }

            if (!placeholderSet.Contains(field.Key))
            {
                throw new InvalidDataException($"Template '{template.Id}' field '{field.Key}' is not present in body placeholders.");
            }
        }
    }
}
