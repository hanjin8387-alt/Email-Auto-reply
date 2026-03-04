using System;
using System.Collections.Generic;
using System.Linq;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class TemplateService : ITemplateService
{
    private readonly ITemplateCatalogLoader _loader;
    private readonly ITemplatePlaceholderValidator _validator;
    private readonly ITemplateRenderer _renderer;
    private readonly ILogger<TemplateService> _logger;
    private readonly Lazy<IReadOnlyList<ReplyTemplate>> _templates;

    public TemplateService(
        ITemplateCatalogLoader loader,
        ITemplatePlaceholderValidator validator,
        ITemplateRenderer renderer,
        ILogger<TemplateService> logger)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _logger = logger ?? NullLogger<TemplateService>.Instance;
        _templates = new Lazy<IReadOnlyList<ReplyTemplate>>(LoadTemplates);
    }

    public List<ReplyTemplate> GetTemplates()
    {
        var templates = _templates.Value
            .Select(CloneTemplate)
            .ToList();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Templates listed: {Count}.", templates.Count);
        }

        return templates;
    }

    public IReadOnlyList<string> ExtractPlaceholders(string templateBody)
        => _renderer.ExtractPlaceholders(templateBody);

    public string FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values)
        => _renderer.FillTemplate(templateBody, values);

    private IReadOnlyList<ReplyTemplate> LoadTemplates()
    {
        var templates = _loader.LoadTemplates();
        _validator.ValidateTemplates(templates);
        return templates
            .Select(CloneTemplate)
            .ToArray();
    }

    private static ReplyTemplate CloneTemplate(ReplyTemplate source)
    {
        return new ReplyTemplate
        {
            Id = source.Id,
            Title = source.Title,
            BodyContent = source.BodyContent,
            Fields = (source.Fields ?? Array.Empty<ReplyTemplateField>())
                .Select(field => new ReplyTemplateField
                {
                    Key = field.Key,
                    Label = field.Label,
                    IsRequired = field.IsRequired,
                    Placeholder = field.Placeholder,
                })
                .ToArray(),
        };
    }
}
