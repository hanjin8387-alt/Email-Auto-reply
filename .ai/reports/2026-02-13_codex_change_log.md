# Codex Change Log — MailTriageAssistant
Date: 2026-02-15
Plan: `.ai/plans/2026-02-13_feature_master_plan.md`

## Regression Status
- Build: OK (`dotnet build MailTriageAssistant/MailTriageAssistant.csproj`)
- Tests: OK (86 passed) (`dotnet test MailTriageAssistant.Tests/`)

## Key Changes (High Level)
- Security: PII redaction 강화(계좌/여권/IP/URL토큰 등) + 유니코드 정규화(FormKC), Win+V 클립보드 히스토리 제외 + 30초 자동 삭제, Markdown/Template 인젝션 방어, 예외 메시지 직접 노출 방지, New Outlook(`olk.exe`) 차단.
- Reliability/Perf: Outlook COM STA 스레드 + 타임아웃/lock, Restrict+GetFirst/GetNext 최적화, partial failure 허용, 본문 프리페치, RangeObservableCollection 배치 갱신.
- UI/UX: 점수 색상/긴급도 레이블, Empty State/ProgressBar, 카테고리 필터, "Outlook에서 열기", 첨부 아이콘 등.
- Observability: Serilog 파일 로깅 + `ILogger<T>` 주입, `SessionStatsService`(인메모리 통계), `#if DEBUG` Stopwatch 계측(ETW EventSource).
- Guardrails: Banned API Analyzer로 `Console`/`Debug`/`Trace` 사용 방지.

## Log File Location
- `%LOCALAPPDATA%\\MailTriageAssistant\\logs\\MailTriageAssistant-*.log` (일 단위 롤링)

## Publish Notes
- .NET SDK는 기본적으로 WPF trimming을 차단합니다(NETSDK1168). 배포용 publish는 trimming을 끄는 것을 권장합니다:
  - `dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x64 -o dist/MailTriageAssistant-win-x64 -p:PublishTrimmed=false`
  - `dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x86 -o dist/MailTriageAssistant-win-x86 -p:PublishTrimmed=false`
- ZIP 산출물:
  - `dist/MailTriageAssistant-win-x64.zip`
  - `dist/MailTriageAssistant-win-x86.zip`

## Manual E2E Checklist
- Classic Outlook 실행 → "메일 분류 실행" → 50건 로드/표시
- 이메일 선택 → 마스킹된 본문 표시 + 30초 후 클립보드 자동 삭제
- Digest 복사 & Teams 열기 → Teams 열림(https → msteams 폴백) / 실패 시 안내 다이얼로그
- Win+V → 클립보드 히스토리에 Digest 미표시
- New Outlook(olk.exe) 실행 중 → 에러 안내


---

## Perf Master Plan Execution
Plan: `.ai/plans/2026-02-13_perf_master_plan.md`

## [0-1] bench: Add PerfScope instrumentation helper
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Helpers/PerfScope.cs`
- **Lines**: +109 / -0
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Debug-only instrumentation; logs only scope name + elapsed time (no email content).

## [0-2] bench: Add PerfEventSource start/stop events
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Helpers/PerfEventSource.cs`, `MailTriageAssistant/Helpers/PerfScope.cs`
- **Lines**: +33 / -4
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Added ETW start/stop events; PerfScope now emits start/stop.

## [0-3] bench: Use PerfScope in OutlookService hot paths
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Services/OutlookService.cs`
- **Lines**: +5 / -15
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Replaced ad-hoc Stopwatch + PerfEventSource Measure with PerfScope in `FetchInboxHeadersInternal` and `GetBodyInternal`.

## [0-4] bench: Instrument PrefetchTopBodiesAsync with PerfScope
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +2 / -0
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Added timing emission for `prefetch_ms` via `PrefetchTopBodiesAsync`.

## [0-5] bench: Log startup_ms and memory_mb
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/App.xaml.cs`
- **Lines**: +36 / -2
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Debug-only startup timing (OnStartup → MainWindow.Loaded) + WorkingSet64 MB logging; stores `startup_ms` in PerfMetrics.

## [0-6] bench: Write perf_metrics.json on shutdown
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/App.xaml.cs`
- **Lines**: +38 / -0
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Debug-only; writes `%LOCALAPPDATA%\\MailTriageAssistant\\perf_metrics.json` containing timings + startup/exit memory.

## [0-7] bench: Add perf_budget.json
- **Status**: OK Committed
- **Files**: `.ai/perf_budget.json`
- **Lines**: +9 / -0
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Added perf budget guardrails for manual regression checks.

## [1-1] perf: Add batch GetBodies API
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Services/IOutlookService.cs`, `MailTriageAssistant/Services/OutlookService.cs`
- **Lines**: +52 / -0
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: expected fewer COM InvokeAsync calls for prefetch/digest
- **Notes**: Added `GetBodies(entryIds)` that runs a single COM `InvokeAsync` and loops on the STA thread.

## [1-2] perf: Batch prefetch bodies via GetBodies
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`, `MailTriageAssistant.Tests/ViewModels/MainViewModelTests.cs`
- **Lines**: +36 / -4
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: fewer COM calls during prefetch
- **Notes**: `PrefetchTopBodiesAsync` now uses `GetBodies` (single COM call). Status message is shown only if body fetch exceeds 200ms.

## [1-3] perf: Batch digest body loading and await prefetch
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +43 / -19
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: fewer COM calls during digest; less redundant body loading
- **Notes**: Track and await the latest prefetch task, then use `GetBodies` to load only missing bodies before digest generation.

## [1-4] perf: Differential update in LoadEmailsAsync
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +111 / -10
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: reduced UI churn on refresh (avoid full Clear+Reset when possible)
- **Notes**: Reuse existing items by EntryId and update only what's needed; apply minimal Move/Insert/Remove updates under `DeferRefresh()` (initial load uses `AddRange` fast-path).

## [2-1] perf: Precompute redacted sender/subject on AnalyzedItem
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Models/AnalyzedItem.cs`, `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +26 / -1
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: prepares removal of per-row redaction converter work
- **Notes**: Added `RedactedSender`/`RedactedSubject` and populate them during header load (and for existing items when missing).

## [2-2] perf: Bind list sender/subject to pre-redacted properties
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/MainWindow.xaml`
- **Lines**: +2 / -2
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: removed per-row redaction converter work in the list
- **Notes**: List binding now uses `RedactedSender` and `RedactedSubject`.

## [2-3] perf: Remove redundant EmailsView.Refresh() calls
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +17 / -3
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: fewer full view refreshes during background body loading
- **Notes**: Enabled live filtering on `Category` (best-effort) and removed refresh calls from prefetch/digest paths.

## [3-1] perf: Use GeneratedRegex for redaction rules
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Services/RedactionService.cs`
- **Lines**: +41 / -23
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Security Tests**: OK (27/27 Redaction)
- **Perf Before**: n/a
- **Perf After**: reduced regex JIT/overhead for repeated redaction
- **Notes**: Converted 10 regex rules to `[GeneratedRegex]` source-generated regexes.

## [3-2] build: Disable Release PDB generation
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/MailTriageAssistant.csproj`, `MailTriageAssistant/App.xaml.cs`
- **Lines**: +3 / -1
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Publish**: OK (Release `win-x64`, `-p:PublishTrimmed=false`), size ≈ 155.53 MB
- **Perf Before**: n/a
- **Perf After**: smaller publish output (no PDBs) + clean Release builds
- **Notes**: Added `<DebugType>none</DebugType>` (Release). Wrapped debug-only perf fields in `#if DEBUG` to avoid Release warnings.

## [3-3] build: Enable PublishReadyToRun in Release
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/MailTriageAssistant.csproj`
- **Lines**: +1 / -0
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Publish**: OK (Release `win-x64`, `-p:PublishTrimmed=false`), size ≈ 174.78 MB
- **Perf Before**: n/a
- **Perf After**: potentially faster startup (ReadyToRun)
- **Notes**: Added `<PublishReadyToRun>true</PublishReadyToRun>` (Release).

## [4-1] perceived: Add body loading overlay in detail panel
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/MainWindow.xaml`
- **Lines**: +40 / -10
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: improved perceived body-load feedback (no blank panel)
- **Notes**: Added an overlay over the summary box while `IsLoading` and `SelectedEmail.IsBodyLoaded == false`.

## [4-2] perceived: Restore selected email after refresh
- **Status**: Skipped (already implemented)
- **Files**: n/a
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: n/a
- **Notes**: Already implemented in `[1-4] perf: Differential update in LoadEmailsAsync` via `selectedEntryId` restore logic.

## [4-3] perceived: Update auto-refresh status once per minute
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +26 / -3
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: reduced UI noise (minute-granularity countdown)
- **Notes**: Added a 1-minute status timer and render countdown from `NextAutoRefreshAt`.

## [4-4] perceived: Add splash screen
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/App.xaml.cs`, `MailTriageAssistant/MailTriageAssistant.csproj`, `MailTriageAssistant/Resources/Splash.png`
- **Lines**: +60 / -2 (plus binary `Splash.png`)
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: improved perceived startup (immediate visual feedback)
- **Notes**: Shows a lightweight splash window early in `OnStartup` and closes it on `MainWindow.Loaded`.

## [4-5] perceived: Add PrefetchCount setting
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/Models/TriageSettings.cs`, `MailTriageAssistant/appsettings.json`, `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +10 / -1
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: configurable prefetch workload (avoid over-prefetching on slower machines)
- **Notes**: Added `PrefetchCount` (default 10) and use it in `PrefetchTopBodiesAsync`.

## [5-1] perf: Dispose IOptionsMonitor OnChange subscription
- **Status**: OK Committed
- **Files**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **Lines**: +45 / -2
- **Build**: OK (0 warnings)
- **Test**: OK (107/107 passed)
- **Perf Before**: n/a
- **Perf After**: reduced leak risk (proper subscription disposal)
- **Notes**: Store `IOptionsMonitor.OnChange()` return value and implement `IDisposable` to stop timers/cancel CTS and dispose the subscription.
