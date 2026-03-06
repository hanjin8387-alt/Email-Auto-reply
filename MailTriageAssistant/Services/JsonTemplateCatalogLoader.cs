using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class JsonTemplateCatalogLoader : ITemplateCatalogLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IOptionsMonitor<TemplateCatalogOptions> _optionsMonitor;
    private readonly ILogger<JsonTemplateCatalogLoader> _logger;

    public JsonTemplateCatalogLoader(
        IOptionsMonitor<TemplateCatalogOptions> optionsMonitor,
        ILogger<JsonTemplateCatalogLoader> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? NullLogger<JsonTemplateCatalogLoader>.Instance;
    }

    public IReadOnlyList<ReplyTemplate> LoadTemplates()
    {
        var configuredPath = _optionsMonitor.CurrentValue.ReplyTemplatesPath ?? string.Empty;
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Reply template catalog file is missing.", fullPath);
        }

        var json = File.ReadAllText(fullPath);
        var document = JsonSerializer.Deserialize<ReplyTemplateCatalogDocument>(json, s_jsonOptions)
            ?? throw new InvalidDataException("Reply template catalog is empty or invalid JSON.");
        ValidateDocument(document, fullPath);
        var templates = document.Templates ?? Array.Empty<ReplyTemplate>();

        var immutable = templates
            .Where(static t => t is not null)
            .Select(CloneTemplate)
            .ToArray();

        _logger.LogInformation(
            "Reply templates loaded from external catalog ({Count}, schemaVersion={SchemaVersion}).",
            immutable.Length,
            document.SchemaVersion);
        return immutable;
    }

    private static void ValidateDocument(ReplyTemplateCatalogDocument document, string fullPath)
    {
        if (document.SchemaVersion != ReplyTemplateCatalogDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Reply template catalog schemaVersion '{document.SchemaVersion}' is not supported for '{fullPath}'.");
        }

        foreach (var template in document.Templates ?? Array.Empty<ReplyTemplate>())
        {
            if (template is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(template.Id))
            {
                throw new InvalidDataException($"Reply template catalog '{fullPath}' contains a template with an empty id.");
            }

            if (string.IsNullOrWhiteSpace(template.BodyContent))
            {
                throw new InvalidDataException(
                    $"Reply template catalog '{fullPath}' template '{template.Id}' has empty bodyContent.");
            }
        }
    }

    private static ReplyTemplate CloneTemplate(ReplyTemplate template)
    {
        return new ReplyTemplate
        {
            Id = template.Id ?? string.Empty,
            Title = template.Title ?? string.Empty,
            BodyContent = template.BodyContent ?? string.Empty,
            Fields = (template.Fields ?? Array.Empty<ReplyTemplateField>())
                .Select(field => new ReplyTemplateField
                {
                    Key = field.Key ?? string.Empty,
                    Label = field.Label ?? string.Empty,
                    Placeholder = field.Placeholder ?? string.Empty,
                    IsRequired = field.IsRequired,
                    DefaultValue = field.DefaultValue is null
                        ? new ReplyTemplateFieldDefaultValue()
                        : new ReplyTemplateFieldDefaultValue
                        {
                            Kind = field.DefaultValue.Kind,
                            Value = field.DefaultValue.Value ?? string.Empty,
                            OffsetDays = field.DefaultValue.OffsetDays,
                            DateFormat = field.DefaultValue.DateFormat ?? "yyyy-MM-dd",
                        },
                })
                .ToArray(),
        };
    }
}
