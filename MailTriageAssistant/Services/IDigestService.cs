using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IDigestService
{
    string GenerateDigest(IReadOnlyList<DigestEmailItem> items);
}
