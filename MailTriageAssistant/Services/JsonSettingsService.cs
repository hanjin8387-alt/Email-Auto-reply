using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private const int MaxEmailLength = 254;
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly string _backupPath;

    public JsonSettingsService()
        : this(GetDefaultSettingsPath())
    {
    }

    public JsonSettingsService(string settingsPath)
    {
        _settingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : settingsPath;
        _backupPath = $"{_settingsPath}.bak";
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
            var model = await LoadSettingsModelAsync(_settingsPath, ct).ConfigureAwait(false);
            return NormalizeVipSenders(model.VipSenders);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            var corruptBackupPath = await BackupCorruptSettingsAsync(_settingsPath, ct).ConfigureAwait(false);
            if (File.Exists(_backupPath))
            {
                try
                {
                    var recoveredModel = await LoadSettingsModelAsync(_backupPath, ct).ConfigureAwait(false);
                    await SaveSettingsModelAtomicAsync(recoveredModel, ct).ConfigureAwait(false);
                    return NormalizeVipSenders(recoveredModel.VipSenders);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Settings recovery failed after detecting a corrupt settings file.",
                        ex);
                }
            }

            throw new InvalidOperationException(
                $"Settings file is corrupt and was backed up to '{corruptBackupPath}'.");
        }
    }

    public async Task SaveVipSendersAsync(IEnumerable<string> vipSenders, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalized = NormalizeVipSenders(vipSenders ?? Array.Empty<string>());

        var model = new UserSettingsV1
        {
            SchemaVersion = UserSettingsV1.Version,
            SavedAtUtc = DateTimeOffset.UtcNow,
            VipSenders = normalized.ToArray(),
        };

        await SaveSettingsModelAtomicAsync(model, ct).ConfigureAwait(false);
    }

    internal static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MailTriageAssistant", "user_settings.json");
    }

    private async Task<UserSettingsV1> LoadSettingsModelAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var model = JsonSerializer.Deserialize<UserSettingsV1>(json, s_jsonOptions);
        if (model is null || model.SchemaVersion != UserSettingsV1.Version)
        {
            throw new InvalidDataException("Unsupported settings schema version.");
        }

        return model;
    }

    private async Task SaveSettingsModelAtomicAsync(UserSettingsV1 model, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(model, s_jsonOptions);
        var validated = JsonSerializer.Deserialize<UserSettingsV1>(json, s_jsonOptions);
        if (validated is null || validated.SchemaVersion != UserSettingsV1.Version)
        {
            throw new InvalidDataException("Settings validation failed before save.");
        }

        var tempPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            if (File.Exists(_settingsPath))
            {
                File.Copy(_settingsPath, _backupPath, overwrite: true);
                File.Replace(tempPath, _settingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save settings atomically.", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Ignore temp cleanup failures.
            }
        }
    }

    private static IReadOnlyList<string> NormalizeVipSenders(IEnumerable<string> vipSenders)
    {
        return (vipSenders ?? Array.Empty<string>())
            .Select(NormalizeVipEntry)
            .Where(IsValidVipEntry)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<string> BackupCorruptSettingsAsync(string path, CancellationToken ct)
    {
        var corruptPath = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak";
        await using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var destination = new FileStream(corruptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);
        }

        File.Delete(path);
        return corruptPath;
    }

    private static string NormalizeVipEntry(string value)
        => (value ?? string.Empty).Trim();

    private static bool IsValidVipEntry(string value)
        => IsValidEmail(value) || IsValidDomainRule(value);

    private static bool IsValidEmail(string email)
        => !string.IsNullOrWhiteSpace(email)
           && email.Length <= MaxEmailLength
           && EmailValidator.IsValidEmail(email);

    private static bool IsValidDomainRule(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("@", StringComparison.Ordinal))
        {
            return false;
        }

        var domain = value[1..];
        return domain.Length > 2
            && domain.Contains('.')
            && !domain.Contains(' ');
    }
}
