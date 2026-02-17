# Observability / Metrics Report — MailTriageAssistant
> Date: 2026-02-13

## Current Instrumentation

| 계측 방법 | 위치 | 상태 |
|---|---|---|
| `PerfEventSource.Measure()` | `OutlookService.cs:382,477` | ⚠️ `#if DEBUG`에서만 2곳 |
| `#if DEBUG` Stopwatch | `MainViewModel.cs:224,571`, `OutlookService.cs:294,431` | ✅ Hot-Path 4곳 |
| Serilog `LogInformation({ElapsedMs})` | `OutlookService.cs:352,447` | ✅ 시간 포함 로그 2곳 |
| Serilog `LogInformation({Count})` | `MainViewModel.cs:266`, `OutlookService.cs:352` | ✅ 건수 로그 |
| `Process.WorkingSet64` | — | ❌ 미사용 |
| 릴리즈 간 지표 비교 | — | ❌ 체계 없음 |

---

## Findings

| # | 영역 | 파일 | 이슈 | 권장사항 |
|---|---|---|---|---|
| OB-01 | PerfEventSource 격리 | `OutlookService.cs:380-383,475-478` | `PerfEventSource.Measure()` 호출이 `#if DEBUG` 안에서만 실행. **Release에서 ETW 이벤트 0**. 그러나 `IsEnabled()` 체크가 내부에 있어 Release에서도 `false` 반환 시 오버헤드 ~ns | `#if DEBUG` 제거 → Release에서도 ETW 수집 가능하게 |
| OB-02 | 이벤트 단일성 | `PerfEventSource.cs:14-23` | `Measure(name, elapsed)` 단일 이벤트만 정의. 구간 시작/종료 이벤트 없어 **ETW 분석 도구(PerfView)에서 구간 시각화 불가** | `MeasureStart(id)` + `MeasureStop(id, elapsed)` 쌍 추가 |
| OB-03 | Stopwatch 비표준화 | `MainViewModel.cs` 4곳, `OutlookService.cs` 2곳 | 각각 독립적 Stopwatch 패턴. 로그 형식 불일치: `Log.Measure("name", sw.Elapsed)` vs `_logger.LogInformation("...{ElapsedMs}...")` | 공통 비동기 계측 헬퍼 `PerfScope` 클래스 제안 |
| OB-04 | 프리페치 미측정 | `MainViewModel.cs:530-563` | `PrefetchTopBodiesAsync`에 Stopwatch/PerfEventSource 미삽입 | Stopwatch + PerfEventSource 추가 |
| OB-05 | 메모리 미측정 | — | `Process.WorkingSet64`, GC 통계 수집 없음 | 앱 시작/종료 + 주기적(10분) 메모리 로깅 |
| OB-06 | 릴리즈 비교 없음 | — | 릴리즈 간 지표 비교 체계 없음. 수동 확인에 의지 | 앱 종료 시 `perf_metrics.json` 출력 → 릴리즈별 보관 |

---

## Proposed Event Schema (성능 전용)

| 이벤트명 | 트리거 | 페이로드 (PII 미포함) |
|---|---|---|
| `AppStartup` | `App.OnStartup` | `ElapsedMs`, `DotNetVersion`, `IsDebug` |
| `HeadersFetched` | `FetchInboxHeadersInternal` 완료 | `Count`, `ElapsedMs` |
| `BodyFetched` | `GetBodyInternal` 완료 | `EntryIdHash`, `BodyLength`, `ElapsedMs` |
| `PrefetchCompleted` | `PrefetchTopBodiesAsync` 완료 | `Count`, `TotalElapsedMs` |
| `DigestGenerated` | `GenerateDigestAsync` 완료 | `ItemCount`, `ElapsedMs` |
| `MemorySnapshot` | 주기적 (10분) + 종료 | `WorkingSetMB`, `GcGen0`, `GcGen1`, `GcGen2` |
| `AppShutdown` | `App.OnExit` | `SessionDurationMin`, `TotalLoads`, `TotalErrors` |

## Proposed Metrics (인메모리)

| 메트릭명 | 타입 | 수집 위치 | 집계 |
|---|---|---|---|
| `header_load_ms` | Histogram | `MainViewModel.LoadEmailsAsync` | avg, p95, max |
| `body_load_ms` | Histogram | `MainViewModel.LoadSelectedEmailBodyAsync` | avg, p95, max |
| `prefetch_ms` | Histogram | `PrefetchTopBodiesAsync` | avg, p95, max |
| `com_call_ms` | Histogram | `InvokeAsync` | avg, p95, max |
| `load_count` | Counter | `LoadEmailsAsync` | 세션 합계 |
| `error_count` | Counter | 전체 catch | 세션 합계 |

### PerfScope 헬퍼 제안

```csharp
internal readonly struct PerfScope : IDisposable
{
    private readonly Stopwatch _sw;
    private readonly string _name;
    private readonly ILogger _logger;

    public PerfScope(string name, ILogger logger)
    {
        _name = name;
        _logger = logger;
        _sw = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _sw.Stop();
        PerfEventSource.Log.Measure(_name, _sw.ElapsedMilliseconds);
        _logger.LogInformation("{Name} completed in {ElapsedMs}ms.", _name, _sw.ElapsedMilliseconds);
    }
}
```

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `Helpers/PerfScope.cs` (신규) | `PerfScope` IDisposable 구조체 — Stopwatch + PerfEventSource + ILogger 통합 | `dotnet build` | 빌드 성공 | Low |
| T-02 | `PerfEventSource.cs` | `MeasureStart(int id, string name)` + `MeasureStop(int id, long elapsed)` 이벤트 쌍 추가 | `dotnet build` | 빌드 성공 | Low |
| T-03 | `OutlookService.cs:380-383,475-478` | `#if DEBUG` 제거 → Release에서도 PerfEventSource 활성화 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| T-04 | `MainViewModel.cs:PrefetchTopBodiesAsync` | `PerfScope` 적용 | `dotnet build` | 빌드 성공 + 로그 출력 | Low |
| T-05 | `App.xaml.cs:OnStartup/OnExit` | `PerfScope("AppStartup")` + 종료 시 `perf_metrics.json` 출력 | `dotnet build` | 빌드 성공 + JSON 파일 생성 | Low |
| T-06 | `App.xaml.cs` | 10분 주기 메모리 스냅샷 (WorkingSet, GC stats) → Serilog | `dotnet build` | 빌드 성공 | Low |
