using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface ITemplateCatalogLoader
{
    IReadOnlyList<ReplyTemplate> LoadTemplates();
}
