# Performance & Reliability Report — MailTriageAssistant
> Date: 2026-02-13
> Reviewer: Agent 06 (Perf & Reliability)

## Summary
- 총 이슈: 8 | Critical: 1 | Major: 3 | Minor: 3 | Info: 1

## Performance Baseline (추정)

| 지표 | 현재 (추정) | 목표 | 상태 |
|---|---|---|---|
| 헤더 50건 로드 | ~800-1500ms (Restrict+GetFirst/GetNext 적용됨) | < 1000ms | ⚠️ 경계 |
| 본문 1건 로드 | ~100-250ms | < 200ms | ✅ 양호 |
| Digest 생성 (프리페치 후) | ~200-400ms | < 500ms | ✅ 양호 |
| Digest 생성 (미프리페치) | ~2000-3000ms (순차 GetBody ×10) | < 500ms | ⚠️ 프리페치 의존 |
| 앱 시작 시간 | ~2-4s (STA 스레드 + COM 초기화) | < 3s | ⚠️ 경계 |

---

## Findings

### 🔴 Critical

| # | 영역 | 파일:함수 | 이슈 | 영향 | 권장사항 |
|---|---|---|---|---|---|
| C-01 | 비동기 | `OutlookService.cs:InvokeAsync` | `CancellationToken` 미지원. 사용자가 로딩 취소 불가. 자동 분류 타이머와 수동 분류 동시 실행 시 **경합 가능** | UI hung 상태 지속 | 모든 `InvokeAsync`에 `CancellationToken` 전파 + 취소 시 `OperationCanceledException` |

### 🟡 Major

| # | 영역 | 파일:함수 | 이슈 | 영향 | 권장사항 |
|---|---|---|---|---|---|
| M-01 | 동시성 | `OutlookService` 전체 | 자동 분류 도입 시 타이머 콜백과 수동 `LoadEmails`가 **동시에 COM 스레드에 진입** 가능. `SemaphoreSlim`으로 직렬화 필요 | 이중 실행, COM 불안정 | `private readonly SemaphoreSlim _gate = new(1, 1)` + `InvokeAsync` 진입 시 acquire |
| M-02 | 앱 시작 | `OutlookService` 생성자 | STA 스레드 생성 후 `tcs.Task.GetAwaiter().GetResult()` 동기 블로킹. DI 해결 시점에 **UI 스레드 ~50-100ms 블록** | 앱 시작 지연 | `Lazy<Task<OutlookService>>` 팩토리 패턴 또는 `IHostedService` |
| M-03 | 프리페치 | `MainViewModel.PrefetchTopBodiesAsync` | 프리페치가 모든 로드 후 fire-and-forget. **자동 분류 주기마다 다시 프리페치** → 불필요한 COM 호출 | COM 부하 | `IsBodyLoaded` 체크 강화 + 이미 프리페치된 항목 스킵 (현재 구현됨 ✅ → 확인) |

### 🟢 Minor

| # | 영역 | 파일:함수 | 이슈 | 영향 | 권장사항 |
|---|---|---|---|---|---|
| m-01 | GC | `TriageService.ContainsAny` | `string.IndexOf` 루프 — 현재 규모(~30 키워드 × 50 이메일)에서 ~50ms 이내. 확장 시 `SearchValues<string>` (.NET 9+) 고려 | 미미 | 현재 규모에서 변경 불필요, 키워드 100개 초과 시 재검토 |
| m-02 | 빌드 | `.csproj` | Release 빌드에 `PublishTrimmed + SingleFile` 이미 적용됨 ✅. `TrimMode=partial`이 적절 | — | 현 상태 유지 |
| m-03 | UI | `MainWindow.xaml` | ListBox 가상화 이미 적용됨 (`VirtualizingStackPanel.IsVirtualizing=True, Recycling`) ✅ | — | 현 상태 유지 |

### ⚪ Info

| # | 영역 | 이슈 |
|---|---|---|
| I-01 | 긍정 | `Restrict + GetFirst/GetNext` COM 패턴 적용 ✅, `RangeObservableCollection` Batch 갱신 ✅, COM 타임아웃 ✅, partial failure ✅ |

---

## Reliability Matrix

| 시나리오 | 현재 처리 | 상태 | 권장사항 |
|---|---|---|---|
| Outlook 미실행 | `EnsureClassicOutlookOrThrow` → 에러 메시지 | ✅ | — |
| Outlook 중간 종료 | `COMException` → `ResetConnection` | ✅ | — |
| New Outlook 감지 | 프로세스 검사 1회 캐싱 | ✅ | — |
| COM Timeout | 30초 타임아웃 → 에러 메시지 | ✅ | — |
| 개별 아이템 실패 | per-item try-catch → skip | ✅ | — |
| **자동 분류 + 수동 동시 실행** | **미처리** | ❌ | SemaphoreSlim 직렬화 |
| **앱 종료 중 자동 분류 진행** | **미처리** | ❌ | CancellationToken + Dispose에서 취소 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 예상 효과 | 테스트 커맨드 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `IOutlookService.cs`, `OutlookService.cs` | 모든 메서드에 `CancellationToken` 파라미터 추가, `InvokeAsync` 내 취소 검사 | 사용자 취소 가능, 자동 분류 중단 가능 | `dotnet build && dotnet test` | High |
| T-02 | `OutlookService.cs` | `SemaphoreSlim _gate` 추가 → `InvokeAsync` 진입 시 acquire, 완료 시 release | 자동/수동 동시 실행 방지 | `dotnet build` | Medium |
| T-03 | `OutlookService` 생성자 | Lazy 초기화 패턴 → DI 해결 시 블로킹 제거 | 앱 시작 ~100ms 단축 | `dotnet build` | Medium |
| T-04 | `MainViewModel.cs` | 자동 분류 타이머에 `CancellationTokenSource` 연계, AppExit 시 Cancel | 안전한 종료 | `dotnet build` | Low |
