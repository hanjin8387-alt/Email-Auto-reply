using System.Collections.Generic;
using System.Threading.Tasks;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IOutlookService
{
    Task<List<RawEmailHeader>> FetchInboxHeaders();
    Task<string> GetBody(string entryId);
    Task CreateDraft(string to, string subject, string body);
}

