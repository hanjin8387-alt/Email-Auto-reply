using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        var document = JsonSerializer.Deserialize<ReplyTemplateDocument>(json, s_jsonOptions);
        var templates = document?.Templates ?? Array.Empty<ReplyTemplate>();

        var immutable = templates
            .Where(static t => t is not null)
            .Select(CloneTemplate)
            .ToArray();

        _logger.LogInformation("Reply templates loaded from external catalog ({Count}).", immutable.Length);
        return immutable;
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
                })
                .ToArray(),
        };
    }

    private sealed class ReplyTemplateDocument
    {
        public ReplyTemplate[] Templates { get; set; } = Array.Empty<ReplyTemplate>();
    }
}
