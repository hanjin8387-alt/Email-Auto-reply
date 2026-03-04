using System;
using MailTriageAssistant.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class DigestDeliveryService : IDigestDeliveryService
{
    private readonly ClipboardSecurityHelper _clipboardHelper;
    private readonly IExternalLauncher _externalLauncher;
    private readonly ILogger<DigestDeliveryService> _logger;

    public DigestDeliveryService(
        ClipboardSecurityHelper clipboardHelper,
        IExternalLauncher externalLauncher,
        ILogger<DigestDeliveryService> logger)
    {
        _clipboardHelper = clipboardHelper ?? throw new ArgumentNullException(nameof(clipboardHelper));
        _externalLauncher = externalLauncher ?? throw new ArgumentNullException(nameof(externalLauncher));
        _logger = logger ?? NullLogger<DigestDeliveryService>.Instance;
    }

    public void CopyDigestToClipboard(string digest)
    {
        _clipboardHelper.SecureCopy(digest ?? string.Empty);
    }

    public bool TryOpenTeams(string? userEmail = null)
    {
        var email = (userEmail ?? string.Empty).Trim();
        if (!EmailValidator.IsValidEmail(email))
        {
            email = string.Empty;
        }

        var https = string.IsNullOrWhiteSpace(email)
            ? "https://teams.microsoft.com"
            : $"https://teams.microsoft.com/l/chat/0/0?users={Uri.EscapeDataString(email)}";

        var msteams = string.IsNullOrWhiteSpace(email)
            ? "msteams:"
            : $"msteams:/l/chat/0/0?users={Uri.EscapeDataString(email)}";

        _logger.LogInformation("TryOpenTeams requested (hasUserEmail={HasUserEmail}).", !string.IsNullOrWhiteSpace(email));

        if (_externalLauncher.TryLaunch(https))
        {
            _logger.LogInformation("Teams opened via https.");
            return true;
        }

        if (_externalLauncher.TryLaunch(msteams))
        {
            _logger.LogInformation("Teams opened via msteams.");
            return true;
        }

        _logger.LogWarning("Failed to open Teams via https and msteams.");
        return false;
    }

}
