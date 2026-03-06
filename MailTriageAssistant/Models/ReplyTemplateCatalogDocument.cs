using System;
using System.Collections.Generic;

namespace MailTriageAssistant.Models;

public sealed class ReplyTemplateCatalogDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; }

    public IReadOnlyList<ReplyTemplate> Templates { get; init; } = Array.Empty<ReplyTemplate>();
}
