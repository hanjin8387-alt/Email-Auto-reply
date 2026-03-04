using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public sealed class JsonSettingsServiceTests
{
    [Fact]
    public async Task SaveVipSendersAsync_ShouldWriteAtomicallyAndCreateBackupOnReplace()
    {
        using var tempDir = new TempDirectory();
        var path = Path.Combine(tempDir.Path, "user_settings.json");
        var service = new JsonSettingsService(path);

        await service.SaveVipSendersAsync(new[] { "a@company.com", "@company.com" });
        await service.SaveVipSendersAsync(new[] { "b@company.com" });

        File.Exists(path).Should().BeTrue();
        File.Exists(path + ".bak").Should().BeTrue();
        Directory.GetFiles(tempDir.Path, "*.tmp").Should().BeEmpty();

        var loaded = await service.LoadVipSendersAsync();
        loaded.Should().ContainSingle().Which.Should().Be("b@company.com");
    }

    [Fact]
    public async Task LoadVipSendersAsync_WhenCorrupt_ShouldBackupAndRecoverFromBackup()
    {
        using var tempDir = new TempDirectory();
        var path = Path.Combine(tempDir.Path, "user_settings.json");
        var service = new JsonSettingsService(path);

        await service.SaveVipSendersAsync(new[] { "first@company.com" });
        await service.SaveVipSendersAsync(new[] { "second@company.com" });

        await File.WriteAllTextAsync(path, "{ not-valid-json");

        var loaded = await service.LoadVipSendersAsync();

        loaded.Should().ContainSingle().Which.Should().Be("first@company.com");
        Directory.GetFiles(tempDir.Path, "user_settings.json.corrupt-*.bak").Should().NotBeEmpty();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MailTriageAssistant.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
