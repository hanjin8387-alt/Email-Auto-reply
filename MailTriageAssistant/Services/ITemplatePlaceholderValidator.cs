using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface ITemplatePlaceholderValidator
{
    void ValidateTemplates(IReadOnlyList<ReplyTemplate> templates);
}
