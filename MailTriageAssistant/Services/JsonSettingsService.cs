using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MailTriageAssistant.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private const int MaxEmailLength = 254;
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _settingsPath;

    public JsonSettingsService()
        : this(GetDefaultSettingsPath())
    {
    }

    public JsonSettingsService(string settingsPath)
    {
        _settingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : settingsPath;
    }

    public async Task<IReadOnlyList<string>> LoadVipSendersAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(_settingsPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath, ct).ConfigureAwait(false);
            var model = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions());
            var list = model?.VipSenders ?? Array.Empty<string>();

            return list
                .Select(NormalizeEmail)
                .Where(IsValidEmail)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Do not surface file contents or exception messages (could include user emails).
            return Array.Empty<string>();
        }
    }

    public async Task SaveVipSendersAsync(IEnumerable<string> vipSenders, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var normalized = (vipSenders ?? Array.Empty<string>())
            .Select(NormalizeEmail)
            .Where(IsValidEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var model = new SettingsModel { VipSenders = normalized };
            var json = JsonSerializer.Serialize(model, JsonOptions());
            await File.WriteAllTextAsync(_settingsPath, json, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Do not include ex.Message.
            throw new InvalidOperationException("설정 파일을 저장할 수 없습니다.");
        }
    }

    internal static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MailTriageAssistant", "user_settings.json");
    }

    private static string NormalizeEmail(string email)
        => (email ?? string.Empty).Trim();

    private static bool IsValidEmail(string email)
        => !string.IsNullOrWhiteSpace(email)
           && email.Length <= MaxEmailLength
           && EmailRegex.IsMatch(email);

    private static JsonSerializerOptions JsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

    private sealed class SettingsModel
    {
        public string[] VipSenders { get; set; } = Array.Empty<string>();
    }
}

