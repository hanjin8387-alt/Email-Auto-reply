# Performance Reliability Report — MailTriageAssistant
> Date: 2026-02-13

## Perf Budget (초안)

| 지표 | 상한 | 현재(추정) | 여유 | 비고 |
|---|---|---|---|---|
| `startup_ms` | 2000 | 1500-3000 | ⚠️ 초과 가능 | COM init 의존 |
| `header_load_ms` | 1500 | 800-1500 | ✅ 경계 | Restrict 적용 |
| `body_load_ms` | 300 | 100-250 | ✅ 여유 | |
| `prefetch_ms` | 2000 | 1000-2500 | ⚠️ 초과 가능 | 10건 순차 |
| `digest_ms` (프리페치 후) | 500 | 200-400 | ✅ 여유 | |
| `memory_mb` | 150 | 80-150 | ⚠️ 경계 | COM 메모리 미포함 |
| `publish_size_mb` | 25 | 15-25 | ✅ 경계 | |

---

## Memory Analysis

| 시나리오 | 전(추정) | 후(추정) | 증가 | 상태 |
|---|---|---|---|---|
| 앱 시작 | 40-60 MB | — | — | 기준 |
| 50건 헤더 로드 | — | +5-10 MB | 5-10 MB | ✅ |
| 10건 본문 프리페치 | — | +2-5 MB | 2-5 MB | ✅ |
| 10회 연속 자동 분류 | — | **+??** | **미측정** | ⚠️ |

### 잠재 메모리 릭 징후

| # | 영역 | 파일:함수 | 이슈 | 심각도 |
|---|---|---|---|---|
| MR-01 | COM 미해제 | `OutlookService:FetchInboxHeadersInternal:306-348` | `GetNext()` 반환 객체 `raw`는 루프 진행 시 이전 `current`를 `SafeReleaseComObject`로 해제 ✅ 단, `raw = filteredItems.GetNext()` 후 루프 탈출 시(Count ≥ 50) **마지막 `raw` 해제**: `finally` 블록에서 `SafeReleaseComObject(raw)` ✅ | ✅ 정상 |
| MR-02 | COM 미해제 | `OutlookService:GetBodyInternal:430-480` | `raw = GetItemFromID` → `finally`에서 `SafeReleaseComObject(raw)` ✅ | ✅ 정상 |
| MR-03 | CTS Dispose | `MainViewModel.cs:424-426` | `_autoRefreshCts?.Cancel(); Dispose(); new CTS()` ✅ | ✅ 정상 |
| MR-04 | 타이머 | `MainViewModel.cs:_autoRefreshTimer` | `DispatcherTimer` — `StopAutoRefresh()` 및 `ResetAutoRefreshTimer()`에서 `Stop()` 호출 ✅. `Tick` 이벤트 핸들러 해제는 **미구현** | ⚠️ Minor |
| MR-05 | IOptionsMonitor | `MainViewModel.cs:202` | `_settingsMonitor.OnChange()` — 반환된 `IDisposable` 미보관 → **구독 해제 불가** → ViewModel 수명 동안 유지 (Singleton이면 무해, Transient면 릭) | ⚠️ 확인 필요 |

---

## Findings

| # | 영역 | 파일 | 이슈 | 영향 | 권장사항 |
|---|---|---|---|---|---|
| PR-01 | 반복 안정성 | `MainViewModel.cs:LoadEmailsAsync` | 10회 연속 자동 분류 후 메모리 증가량 **미측정**. `Emails.Clear()`는 C# GC에 의지하지만, COM RCW는 즉시 해제 필요 | 잠재 릭 | `#if DEBUG` 메모리 로깅 추가 → 10회 반복 후 `GC.Collect() + WorkingSet` 비교 |
| PR-02 | IOptionsMonitor 구독 | `MainViewModel.cs:202` | `OnChange()` 반환 `IDisposable` 미보관 | Minor | `_onChangeDisposable = settingsMonitor.OnChange(...)` → Dispose 시 해제 |
| PR-03 | DispatcherTimer 해제 | `MainViewModel.cs:174` | `_autoRefreshTimer.Tick -= OnAutoRefreshTimerTick` 미호출 (ViewModel이 Singleton이라 현재 무해) | Info | Transient 전환 시 `IDisposable` 구현 필요 |
| PR-04 | Perf Budget CI | — | 성능 예산 자동 검증 없음. 수동 측정에 의지 | Medium | `#if DEBUG` 앱 종료 시 지표 파일 출력 → CI에서 임계값 비교 |

---

## Regression Prevention

| 방법 | 구현 난이도 | 효과 |
|---|---|---|
| 앱 종료 시 지표 JSON 출력 (`%LocalAppData%/.../perf_metrics.json`) | Low | 릴리즈 간 비교 가능 |
| `#if DEBUG` 앱 시작/종료 메모리 로깅 | Low | 릭 조기 발견 |
| CI에서 `dotnet test` 후 벤치 스크립트 실행 | Medium | 회귀 자동 감지 |
| Perf Budget 임계값 파일 (`.ai/perf_budget.json`) | Low | 기준점 문서화 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `MainViewModel.cs:202` | `_onChangeDisposable` 필드 + Dispose 패턴 구현 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| T-02 | `App.xaml.cs` / `MainViewModel.cs` | `#if DEBUG` 앱 시작/종료 메모리 로깅 (`Process.WorkingSet64`) | `dotnet build` | 빌드 성공 + 메모리 로그 | Low |
| T-03 | `.ai/perf_budget.json` (신규) | 성능 예산 임계값 파일 | — | 파일 생성 | Low |
| T-04 | `App.xaml.cs` | `#if DEBUG` 앱 종료 시 `perf_metrics.json` 출력 | `dotnet build` | 파일 생성 확인 | Low |
