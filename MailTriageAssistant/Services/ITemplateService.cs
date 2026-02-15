using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface ITemplateService
{
    List<ReplyTemplate> GetTemplates();

    string FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values);
}

