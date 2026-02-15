using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public interface IDigestService
{
    string GenerateDigest(IReadOnlyList<AnalyzedItem> items);

    void OpenTeams(string digest, string? userEmail = null);
}

