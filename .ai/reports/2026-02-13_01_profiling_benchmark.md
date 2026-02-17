# Profiling & Benchmark Report — MailTriageAssistant
> Date: 2026-02-13

## Baseline Metrics

| 지표 | 현재(추정) | 목표 | 측정 방법 | 코드 위치 |
|---|---|---|---|---|
| `startup_ms` | 1500-3000 | ≤ 2000 | Stopwatch (OnStartup → Loaded) | `App.xaml.cs:OnStartup` — **미삽입** |
| `header_load_ms` | 800-1500 | ≤ 1000 | Stopwatch (LoadEmailsAsync) | `MainViewModel.cs:223-324` — `#if DEBUG` ✅ |
| `body_load_ms` | 100-250 | ≤ 200 | Stopwatch (GetBodyInternal) | `OutlookService.cs:431-478` — `#if DEBUG` ✅ |
| `digest_ms` | 200-400 (프리페치 후) | ≤ 500 | Stopwatch (GenerateDigestAsync) | `MainViewModel.cs:570-646` — `#if DEBUG` ✅ |
| `prefetch_ms` | 1000-2500 (10건 순차) | ≤ 1500 | Stopwatch | `MainViewModel.cs:530-563` — **미삽입** |
| `memory_mb` | 80-150 | ≤ 120 | Process.WorkingSet64 | **미삽입** |
| `publish_size_mb` | 15-25 (추정) | ≤ 20 | `dotnet publish` 후 파일 크기 | CLI |
| `com_call_avg_ms` | 10-30 | ≤ 20 | PerfEventSource | `OutlookService.cs:380-383,475-478` — `#if DEBUG` ✅ |

## Measurement Infrastructure

| 계측 포인트 | 상태 | 파일:줄 |
|---|---|---|
| `FetchInboxHeadersInternal` Stopwatch | ✅ 삽입됨 | `OutlookService.cs:294,351-352,380-383` |
| `GetBodyInternal` Stopwatch | ✅ 삽입됨 | `OutlookService.cs:431,446-447,475-478` |
| `LoadEmailsAsync` Stopwatch | ✅ 삽입됨 | `MainViewModel.cs:224,322-324` |
| `GenerateDigestAsync` Stopwatch | ✅ 삽입됨 | `MainViewModel.cs:571,642-644` |
| **앱 시작 시간** | ❌ 미삽입 | `App.xaml.cs:OnStartup` |
| **PrefetchTopBodiesAsync** | ❌ 미삽입 | `MainViewModel.cs:530` |
| **메모리 (WorkingSet)** | ❌ 미삽입 | 없음 |
| `PerfEventSource.Measure()` 호출 | ⚠️ `#if DEBUG`에서만 2곳 | `OutlookService.cs:382,477` |
| Serilog `{ElapsedMs}` 로그 | ✅ `OutlookService.cs:352` | `LogInformation` 포함 |

---

## Findings

### 🔴 Critical

| # | 파일 | 이슈 | 권장사항 |
|---|---|---|---|
| PB-01 | `App.xaml.cs` | **앱 시작 시간 측정 없음**. 최대 병목 후보(STA Thread + COM init)인데 베이스라인 수치 없음 | `OnStartup` 진입 시 `Stopwatch.StartNew()`, `MainWindow.Loaded` 이벤트에서 중지 + 로그 |
| PB-02 | `MainViewModel.cs:530-563` | **PrefetchTopBodiesAsync 계측 없음**. 10건 × GetBody = 추정 1-2.5s인데 측정 불가 | `#if DEBUG` Stopwatch + PerfEventSource 삽입 |

### 🟡 Major

| # | 파일 | 이슈 | 권장사항 |
|---|---|---|---|
| PB-03 | `PerfEventSource.cs` | `Measure(string, long)` 단일 이벤트만 정의. 시작/종료 쌍 이벤트 없어 ETW 분석 도구에서 구간 시각화 불가 | `MeasureStart(id, name)`, `MeasureStop(id, elapsed)` 쌍 추가 |
| PB-04 | 전체 | **메모리 사용량 측정 없음**. COM Interop 메모리 릭 감지 불가 | `App.xaml.cs:OnStartup` + 주기적 `Process.WorkingSet64` 로깅 |

### 🟢 Minor

| # | 파일 | 이슈 | 권장사항 |
|---|---|---|---|
| PB-05 | `OutlookService.cs:382,477` | PerfEventSource 호출이 `#if DEBUG`에서만 실행 → Release에서 ETW 계측 불가 | DEBUG 게이트 제거하되, `IsEnabled()` 체크 (현재 구현됨)가 성능 보호 |
| PB-06 | — | `dotnet publish` 크기 측정 자동화 없음 | `.ai/scripts/measure_publish_size.ps1` 스크립트 작성 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `App.xaml.cs` | 앱 시작 Stopwatch: `OnStartup` → `MainWindow.Loaded` | `dotnet build -c Debug` → 앱 실행 → 로그 확인 | `startup_ms` 출력 | Low |
| T-02 | `MainViewModel.cs:PrefetchTopBodiesAsync` | `#if DEBUG` Stopwatch + PerfEventSource 삽입 | `dotnet build -c Debug` → 앱 실행 → 로그 확인 | `prefetch_ms` 출력 | Low |
| T-03 | `PerfEventSource.cs` | `MeasureStart(int id, string name)`, `MeasureStop(int id, long elapsedMs)` 이벤트 쌍 추가 | `dotnet build` | 빌드 성공 | Low |
| T-04 | `App.xaml.cs` | `Process.WorkingSet64` 시작/종료 로깅 | `dotnet build` | 빌드 성공 + 메모리 로그 | Low |
| T-05 | `OutlookService.cs:380-383,475-478` | PerfEventSource 호출을 `#if DEBUG` 밖으로 이동 (ETW `IsEnabled()` 자체가 보호) | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
