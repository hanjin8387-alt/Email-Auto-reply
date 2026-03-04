using System.Collections.Generic;

namespace MailTriageAssistant.Services;

public interface ITemplateRenderer
{
    IReadOnlyList<string> ExtractPlaceholders(string templateBody);

    string FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values);
}
