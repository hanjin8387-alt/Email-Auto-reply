# Performance Optimization Master Plan — MailTriageAssistant
> Date: 2026-02-13
> Source Reports: `2026-02-13_01_profiling_benchmark` ~ `2026-02-13_08_observability_metrics`
> Total Findings: 39 across 8 agents → **28 unique commit units**

---

## Executive Summary

8개 성능 에이전트 분석 결과, 이 앱은 COM Interop 제약 하에서 이미 상당한 최적화(가상화, Restrict, SemaphoreSlim, RangeObservableCollection, per-item partial failure)가 적용되어 있다. 남은 가장 큰 기회는:

1. **차분 업데이트** (NC-01): 자동 분류 시 전체 Clear+Reload 대신 차분 비교 → **프리페치 1-2.5s 절감 + 깜빡임 제거**
2. **배치 GetBody** (BL-04): 개별 InvokeAsync 대신 1회 COM 호출 내 foreach → **30-90ms/프리페치 절감**
3. **RedactionConverter 제거** (FR-02): 렌더마다 Regex 100회 → 사전 계산 프로퍼티 바인딩 → **50-100ms/렌더 절감**
4. **계측 인프라 통합** (OB-01~06): PerfScope 헬퍼, perf_metrics.json, 릴리즈 비교 기반 확보

---

## Perf Budget (성능 예산)

| 지표 | 상한 | 현재(추정) | 목표 | 여유 |
|---|---|---|---|---|
| `startup_ms` | 2500 | 1500-3000 | ≤ 2000 | ⚠️ |
| `header_load_ms` | 1500 | 800-1500 | ≤ 1000 | ⚠️ |
| `body_load_ms` | 300 | 100-250 | ≤ 200 | ✅ |
| `prefetch_ms` | 2000 | 1000-2500 | ≤ 1500 | ⚠️ |
| `digest_ms` (프리페치 후) | 500 | 200-400 | ≤ 300 | ✅ |
| `memory_mb` | 150 | 80-150 | ≤ 120 | ⚠️ |
| `publish_size_mb` | 25 | 15-25 | ≤ 20 | ⚠️ |

---

## Priority Distribution

| 우선순위 | 정의 | 커밋 수 | 근거 |
|---|---|---|---|
| **P0** | 계측 인프라 — 모든 개선의 전제 | 7 | 측정 없이 최적화 금지 원칙 |
| **P1** | 실제 성능 개선 — 사용자 체감 ×, Stopwatch 수치 ↓ | 10 | 가장 큰 효과 |
| **P2** | 체감속도 개선 — 실제 시간 불변, 사용자 체감 ↑ | 8 | UX 품질 |
| **P3** | 빌드 / 회귀 방지 — 장기 유지보수 | 3 | 안정성 |

---

## Phase 0: 계측 인프라 (P0) — 7 commits

> 목표: 모든 Hot-Path에 재현 가능한 계측 삽입 + Perf Budget 파일 생성

| Commit | 타입 | 변경 파일 | 변경 요지 | 테스트/벤치 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|---|
| 0-1 | bench | `Helpers/PerfScope.cs` (신규) | `PerfScope` IDisposable 구조체: Stopwatch + PerfEventSource + ILogger 통합 | `dotnet build` | 빌드 성공 | Low |
| 0-2 | bench | `PerfEventSource.cs` | `MeasureStart(id, name)` + `MeasureStop(id, elapsed)` 이벤트 쌍 추가 | `dotnet build` | 빌드 성공 | Low |
| 0-3 | bench | `OutlookService.cs:380-383,475-478` | `#if DEBUG` Stopwatch/PerfEventSource → `#if DEBUG` 제거, PerfScope 적용 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 0-4 | bench | `MainViewModel.cs:PrefetchTopBodiesAsync` | PerfScope 삽입 (누락 포인트) | `dotnet build` | 빌드 성공 + prefetch_ms 출력 | Low |
| 0-5 | bench | `App.xaml.cs` | 앱 시작 Stopwatch: `OnStartup` → `MainWindow.Loaded`. 메모리 로깅 (`WorkingSet64`) | `dotnet build` | startup_ms + memory_mb 출력 | Low |
| 0-6 | bench | `App.xaml.cs` | 앱 종료 시 `perf_metrics.json` 출력 (`%LocalAppData%/MailTriageAssistant/`) | `dotnet build` | JSON 파일 생성 | Low |
| 0-7 | bench | `.ai/perf_budget.json` (신규) | 성능 예산 임계값 파일 | — | 파일 생성 | Low |

**Phase 0 DoD**: `dotnet build && dotnet test` Green, Debug 모드 실행 시 모든 Hot-Path 시간 출력, `perf_metrics.json` 생성.

---

## Phase 1: 실제 성능 — 데이터 파이프라인 (P1) — 4 commits

> 목표: 차분 업데이트 + 배치 COM + 프리페치 최적화

| Commit | 타입 | 변경 파일 | 변경 요지 | 테스트/벤치 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|---|
| 1-1 | perf | `IOutlookService.cs`, `OutlookService.cs` | `GetBodies(IReadOnlyList<string> entryIds)` 배치 메서드. 1회 `InvokeAsync` 내 foreach | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 1-2 | perf | `MainViewModel.cs:PrefetchTopBodiesAsync` | `GetBodies` 배치 호출로 전환. 진행률 StatusMessage 추가 | `dotnet build && dotnet test` | 빌드+테스트 통과 + StatusMessage 갱신 | Medium |
| 1-3 | perf | `MainViewModel.cs:GenerateDigestAsync` | `_prefetchTask` await + 미로드분 `GetBodies` 배치 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 1-4 | perf | `MainViewModel.cs:LoadEmailsAsync` | **차분 업데이트**: 기존 EntryId 세트 vs 신규 비교 → 신규만 추가, 삭제 항목 제거, 기존(IsBodyLoaded) 재활용 | `dotnet build && dotnet test` | 빌드+테스트 통과 + 자동 분류 시 캐시 유지 | High |

**Phase 1 DoD**: `dotnet build && dotnet test` Green, `prefetch_ms` 30% 이상 단축 확인 (Debug 앱).

---

## Phase 2: 실제 성능 — 렌더링 (P1) — 3 commits

> 목표: RedactionConverter 제거 + Refresh 최적화

| Commit | 타입 | 변경 파일 | 변경 요지 | 테스트/벤치 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|---|
| 2-1 | perf | `Models/AnalyzedItem.cs`, `MainViewModel.cs` | `RedactedSender`, `RedactedSubject` 프로퍼티 추가. LoadEmails/Prefetch에서 사전 계산 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 2-2 | perf | `MainWindow.xaml` | Sender/Subject 바인딩을 직접 프로퍼티로 변경, RedactionConverter 사용 제거 (목록 영역) | `dotnet build` | 빌드 성공 + Sender 마스킹 유지 | Medium |
| 2-3 | perf | `MainViewModel.cs` | `EmailsView.Refresh()` 중복 호출 제거 (4곳 → 2곳) | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |

**Phase 2 DoD**: `dotnet build && dotnet test` Green, Converter 호출 100회 → 0회 (목록).

---

## Phase 3: 실제 성능 — Regex + 빌드 (P1) — 3 commits

> 목표: GeneratedRegex + 빌드 최적화

| Commit | 타입 | 변경 파일 | 변경 요지 | 테스트/벤치 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|---|
| 3-1 | perf | `RedactionService.cs` | 10개 정규식에 `[GeneratedRegex]` source generator 적용 | `dotnet build && dotnet test` | 빌드+테스트 통과 + PII 보안테스트 Green | Medium |
| 3-2 | build | `.csproj` | Release: `<DebugType>none</DebugType>` | `dotnet publish` → 크기 확인 | 크기 감소 | Low |
| 3-3 | build | `.csproj` | `<PublishReadyToRun>true</PublishReadyToRun>` (Release) | `dotnet publish` → 앱 시작 비교 | 시작 시간 단축 | Medium |

**Phase 3 DoD**: `dotnet build && dotnet test` Green, PII 보안테스트 Green, publish 크기 ≤ 이전.

---

## Phase 4: 체감속도 — 즉각 피드백 (P2) — 5 commits

> 목표: 사용자 체감속도 향상 (실제 연산 불변)

| Commit | 타입 | 변경 파일 | 변경 요지 | 테스트/벤치 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|---|
| 4-1 | perceived | `MainWindow.xaml` | 본문 영역 로딩 오버레이 (`IsLoading` 바인딩 Grid) | `dotnet build` | 빌드 성공 + 시각 확인 | Low |
| 4-2 | perceived | `MainViewModel.cs` | 재로드 후 이전 `SelectedEmail.EntryId` 복원 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 4-3 | perceived | `MainViewModel.cs:UpdateAutoRefreshStatusText` | 자동 분류 카운트다운 (1분마다 갱신) | `dotnet build` | 빌드 성공 | Low |
| 4-4 | perceived | `App.xaml.cs` + 이미지 | SplashScreen 추가 | `dotnet build` | 빌드 성공 + 스플래시 | Medium |
| 4-5 | perceived | `Models/TriageSettings.cs`, `appsettings.json` | `PrefetchCount: int = 10` 설정 추가 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |

**Phase 4 DoD**: `dotnet build && dotnet test` Green, 시각 확인.

---

## Phase 5: 안정성 + 회귀 방지 (P3) — 3 commits

> 목표: 릭 수정 + 회귀 방지 체계

| Commit | 타입 | 변경 파일 | 변경 요지 | 테스트/벤치 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|---|
| 5-1 | perf | `MainViewModel.cs:202` | `IOptionsMonitor.OnChange()` 반환 `IDisposable` 보관 + Dispose 패턴 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 5-2 | bench | `App.xaml.cs` | 10분 주기 메모리 스냅샷 로깅 (WorkingSet, GC) | `dotnet build` | 빌드 성공 | Low |
| 5-3 | build | `OutlookService:헤더 TTL` | 30초 TTL 캐시 — 짧은 간격 재호출 방지 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |

**Phase 5 DoD**: `dotnet build && dotnet test` Green, 메모리 로깅 동작.

---

## Phase 6: 검증 — 22 commits

> 목표: 전체 지표 측정 + 예산 준수 확인

| Commit | 변경 요지 | 테스트/벤치 |
|---|---|---|
| 6-1 | Debug 앱 실행 → `perf_metrics.json` 수집 → Phase 0 대비 비교 표 작성 | 수동 확인 |
| 6-2 | `.ai/reports/2026-02-13_codex_change_log.md` 완성 | — |

---

## Test Strategy

### 빌드 + 단위 테스트
```bash
dotnet build MailTriageAssistant/MailTriageAssistant.csproj
dotnet test MailTriageAssistant.Tests/
```

### 벤치마크 (수동)
```bash
# Debug 모드 앱 실행 → 콘솔/로그에서 PerfScope 출력 확인
dotnet build MailTriageAssistant/MailTriageAssistant.csproj -c Debug
# 앱 실행 후 "메일 분류 실행" 클릭 → 로그파일 확인
# %LocalAppData%/MailTriageAssistant/logs/
```

### Publish 크기
```powershell
dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x64 --self-contained -o ./publish_measure
(Get-ChildItem ./publish_measure -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
```

---

## Guardrails

### 성능 예산 파일 (`.ai/perf_budget.json`)
```json
{
  "startup_ms": 2500,
  "header_load_ms": 1500,
  "body_load_ms": 300,
  "prefetch_ms": 2000,
  "digest_ms": 500,
  "memory_mb": 150,
  "publish_size_mb": 25
}
```

### 롤백 기준
1. **빌드 실패**: `git revert HEAD` 즉시
2. **테스트 2회 연속 실패**: 블로커 리포트 → Task 스킵
3. **PII 보안 테스트 실패**: `git revert HEAD` 즉시
4. **성능 회귀 20% 이상**: `git revert HEAD` + 재설계

### 롤백 절차
```bash
git revert HEAD
echo "Reverted: {commit_hash} — {사유}" >> .ai/reports/2026-02-13_codex_change_log.md
```

---

## Codex Instructions — 체크리스트

### 1. 성능 규칙
- [ ] 측정 전 코드 수정 금지 (Phase 0 완료 확인)
- [ ] 1 commit = 1 개선 (혼합 금지)
- [ ] 성능 목적 외 기능 변경 금지
- [ ] `#if DEBUG` 계측 코드는 Release 빌드에 영향 0
- [ ] Perf Budget 상한 초과 시 커밋 거부

### 2. 보안 불변 규칙
- [ ] 이메일 본문을 디스크/로그에 저장 금지
- [ ] PII 마스킹 없이 클립보드 복사 금지
- [ ] 외부 API 호출 금지
- [ ] `[GeneratedRegex]` 전환 시 기존 PII 보안 테스트 Green 필수

### 3. 커밋 규칙
- [ ] 커밋 메시지: `[Phase-Commit] 타입: 설명`
- [ ] 타입: `perf` | `perceived` | `bench` | `build`
- [ ] 커밋당 변경 파일 ≤ 5개
- [ ] 커밋당 변경 행 ≤ 200행

### 4. 빌드/테스트 게이트
```bash
dotnet build MailTriageAssistant/MailTriageAssistant.csproj
dotnet test MailTriageAssistant.Tests/
```
