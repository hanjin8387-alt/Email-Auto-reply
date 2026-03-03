# AGENTS.md — MailTriageAssistant Project Briefing

## Project Overview

**MailTriageAssistant** is a WPF desktop application (.NET 8) that connects to Microsoft Outlook via COM Interop, fetches inbox emails, scores and categorizes them by priority, and provides digest generation + Teams integration.

## Tech Stack

- **Runtime**: .NET 8.0 (net8.0-windows)
- **UI**: WPF + MVVM (no third-party MVVM framework)
- **DI**: `Microsoft.Extensions.DependencyInjection`
- **Logging**: Serilog (file sink, 7-day rotation)
- **Outlook**: COM Interop via dedicated STA thread
- **Testing**: xUnit 2.9 + FluentAssertions 7 + Moq 4.20
- **Language**: C# 12, Nullable enabled, ImplicitUsings enabled

## Project Structure

```
MailTriageAssistant/               # Main WPF application
├── App.xaml / App.xaml.cs         # Application entry, DI, splash, system tray
├── MainWindow.xaml / .xaml.cs     # Main UI (code-behind is minimal)
├── ViewModels/
│   ├── MainViewModel.cs          # Core business logic (1070 lines)
│   └── RelayCommand.cs           # ICommand implementations
├── Models/
│   ├── AnalyzedItem.cs            # Email data model (INotifyPropertyChanged)
│   ├── RawEmailHeader.cs          # Outlook header DTO
│   ├── EmailCategory.cs           # Enum (7 categories)
│   ├── ReplyTemplate.cs           # Template model
│   └── TriageSettings.cs          # Configuration model
├── Services/
│   ├── OutlookService.cs          # COM Interop (STA thread, caching)
│   ├── TriageService.cs           # Keyword-based scoring
│   ├── DigestService.cs           # Markdown digest + Teams
│   ├── RedactionService.cs        # PII masking (10 regex patterns)
│   ├── TemplateService.cs         # Reply template engine
│   ├── ClipboardSecurityHelper.cs # Auto-clear clipboard
│   ├── JsonSettingsService.cs     # User settings persistence
│   ├── SessionStatsService.cs     # Session telemetry
│   ├── WpfDialogService.cs        # MessageBox wrapper
│   └── I*.cs                      # Service interfaces (7 files)
├── Helpers/
│   ├── PerfScope.cs / PerfMetrics # DEBUG performance tracking
│   ├── PerfEventSource.cs         # ETW EventSource
│   ├── RangeObservableCollection  # Batch-add ObservableCollection
│   ├── TaskExtensions.cs          # SafeFireAndForget
│   └── *Converter.cs              # WPF IValueConverters (4 files)
├── Resources/
│   ├── Strings.ko.xaml            # Korean localization
│   └── Strings.en.xaml            # English localization
└── appsettings.json               # Default configuration

MailTriageAssistant.Tests/          # Test project
├── Helpers/                       # Converter tests (4 files)
├── Services/                      # Service tests (4 files)
├── ViewModels/                    # MainViewModelTests.cs
└── Security/                      # RedactionSecurityTests.cs
```

## Build & Test Commands

```powershell
# Build (treat warnings as errors)
dotnet build --warnaserror MailTriageAssistant/MailTriageAssistant.csproj

# Build tests
dotnet build MailTriageAssistant.Tests/MailTriageAssistant.Tests.csproj

# Run tests
dotnet test MailTriageAssistant.Tests/MailTriageAssistant.Tests.csproj --no-build --verbosity normal
```

## Coding Conventions

1. **Namespace per folder** — file-scoped namespace declarations (`namespace X;`)
2. **Null safety** — Nullable enabled, use `ArgumentNullException.ThrowIfNull`
3. **Logging** — Never log exception `.Message` (may contain PII). Log only `ex.GetType().Name` and `ex.HResult`
4. **Exception handling** — Wrap COM operations in try-catch, use `SafeReleaseComObject` for COM cleanup
5. **Async** — Use `ConfigureAwait(true)` on UI thread, `ConfigureAwait(false)` on background threads
6. **Testing** — xUnit `[Fact]` / `[Theory]`, FluentAssertions `.Should()`, Moq for interfaces
7. **Line endings** — Mixed CRLF/LF (do not normalize)
8. **Language** — UI strings in Korean, code comments and logs in English

## Working Agreements

- Keep changes minimal and focused on the assigned issue
- Always run `dotnet build --warnaserror` before finishing
- Always run `dotnet test` to verify no regressions
- Do not modify test files unless explicitly assigned to do so
- Preserve existing coding style and naming conventions
