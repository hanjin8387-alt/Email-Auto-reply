# Performance / Perceived-Speed Optimization Cycle — MailTriageAssistant
> Version: 1.0 · Date: 2026-02-13

---

## Assumptions

| 항목 | 값 | 근거 |
|---|---|---|
| Runtime | .NET 8, WPF (Windows Desktop) | `MailTriageAssistant.csproj` — `net8.0-windows`, `<UseWPF>true` |
| Architecture | MVVM + DI (Microsoft.Extensions) | `App.xaml.cs`, `ViewModels/`, `Services/` |
| Hot-Path #1 | **앱 시작** → STA Thread + COM init | `OutlookService` 생성자 |
| Hot-Path #2 | **헤더 로드** → COM Restrict+GetFirst/GetNext ×50 | `FetchInboxHeadersInternal` |
| Hot-Path #3 | **본문 로드** → COM GetItemFromID ×1~10 | `GetBodyInternal`, `PrefetchTopBodiesAsync` |
| Hot-Path #4 | **Digest 생성** → 본문 프리페치 + StringBuilder | `GenerateDigestAsync` |
| Hot-Path #5 | **UI 렌더** → ListBox (가상화 적용) + 필터 | `MainWindow.xaml`, `EmailsView.Refresh` |
| Build | `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` | SDK-style |
| Test | `dotnet test MailTriageAssistant.Tests/` | xUnit + Moq |
| Profiling | `#if DEBUG` Stopwatch + `PerfEventSource` ETW | 이미 구현, 미활용 |
| Publish | Trimmed + SingleFile + SelfContained (Release) | `.csproj` PropertyGroup |
| COM 제약 | Outlook COM은 STA 스레드 필수, 병렬 호출 불가, 평균 10-30ms/call | `SemaphoreSlim` 직렬화 |

---

## 핵심 원칙

1. **측정 없이 최적화 금지** — 베이스라인 미정의 시 코드 수정 불가
2. **체감속도 vs 실제성능 구분** — 스켈레톤/프리페치는 "체감", COM 최적화는 "실제"
3. **작은 커밋 + 벤치 게이트** — 1 commit = 1 개선, 빌드+테스트+벤치 후 커밋
4. **회귀 방지** — 성능 예산(Perf Budget) 초과 시 커밋 거부
5. **기능 변경 최소** — 성능 목적 외 행동 변경 금지

---

## Cycle Overview

```
[1 베이스라인 정의] → [2 측정 도구 확정] → [3 병목 식별] → [4 개선안 설계]
         ↓ 측정 불가            ↓ 도구 부재           ↓ 병목 없음          ↓ 위험 과다
  [BLOCKER 리포트]       [대안 탐색/수동측정]  [SHIP → Phase 7]    [축소 or 취소]

→ [5 Codex 구현] → [6 성능 회귀 검증] → [7 정리/문서화]
       ↓ 2회 실패            ↓ 회귀 발견
  [BLOCKER + revert]    [revert + 재설계]
```

---

## Phase 1: 베이스라인 정의 (Baseline Definition)

### 목적
현재(변경 전) 성능 지표를 숫자로 기록한다.

### 산출물
- 측정 대상 지표 목록 + 현재 값 (추정 허용, 범위로 기록)
- 목표 값 (개선 목표)

### 지표 후보 (WPF Desktop 앱)

| 지표 | 설명 | 측정 방법 |
|---|---|---|
| `startup_ms` | 앱 시작 → MainWindow 표시 | Stopwatch (OnStartup → Loaded) |
| `header_load_ms` | "메일 분류 실행" 클릭 → 목록 표시 | Stopwatch (LoadEmailsAsync) |
| `body_load_ms` | 이메일 선택 → 본문 표시 | Stopwatch (LoadSelectedEmailBodyAsync) |
| `digest_ms` | Digest 생성 → 클립보드 복사 | Stopwatch (GenerateDigestAsync) |
| `prefetch_ms` | 상위 10건 본문 프리페치 | Stopwatch (PrefetchTopBodiesAsync) |
| `memory_mb` | 앱 Working Set | `Process.WorkingSet64` |
| `com_call_ms` | 개별 COM 호출 시간 | Stopwatch per InvokeAsync |
| `publish_size_mb` | Release 단일 파일 크기 | `dotnet publish` 후 파일 크기 |
| `ui_freeze_ms` | UI 스레드 블로킹 시간 | DispatcherTimer 누적 |

### Definition of Done
- [ ] 5개 이상 지표 정의 + 현재값(추정) + 목표값
- [ ] 측정 커맨드/코드 확정

### 중단 기준
- WPF 앱이라 브라우저 도구(Lighthouse 등) 사용 불가 → `Stopwatch` + `PerfEventSource` + `Process.WorkingSet64` 기반 수동 측정으로 대체

---

## Phase 2: 측정 도구/커맨드 확정 (Measurement Setup)

### 목적
재현 가능한 측정 방법을 확정한다.

### 산출물
- 측정 코드 위치 + 출력 형식
- 벤치마크 실행 커맨드

### 측정 전략 (WPF Desktop)

| 방법 | 대상 | 위치 |
|---|---|---|
| `#if DEBUG` Stopwatch | 모든 Hot-Path | 이미 `LoadEmailsAsync`, `FetchInboxHeadersInternal`, `GetBodyInternal`, `GenerateDigestAsync`에 삽입됨 |
| `PerfEventSource.Log.Measure(name, ms)` | ETW 이벤트 | `PerfEventSource.cs` — 이미 정의됨, 호출 코드 일부 존재 |
| `Process.WorkingSet64` | 메모리 | App.xaml.cs OnStartup / OnExit |
| `dotnet publish -c Release` → 파일 크기 | 빌드 크기 | CLI |

### Definition of Done
- [ ] 측정 코드가 `#if DEBUG` 게이트 안에서 동작
- [ ] `dotnet build -c Debug` 시 측정 활성화, Release 시 제거
- [ ] 최소 1회 수동 측정 결과 기록

---

## Phase 3: 병목 식별 (Bottleneck Identification)

### 목적
측정 데이터를 기반으로 가장 큰 시간 소비 구간을 식별한다.

### 에이전트 (병렬 실행)
| 에이전트 | 분석 영역 |
|---|---|
| 01 Profiling/Benchmark | 베이스라인·지표·프로파일링 포인트 |
| 02 Frontend Rendering | WPF 렌더 병목·가상화·바인딩 |
| 03 Backend Latency | COM 호출·서비스 처리 시간 |
| 04 Network/Cache/Data | 데이터 캐싱·프리페치·배치 |
| 05 Build/Bundle Size | Publish 크기·Trimming |
| 06 Perceived Speed/UX | 체감속도·스켈레톤·로딩 인디케이터 |
| 07 Perf Reliability | 회귀 방지·Perf Budget |
| 08 Observability/Metrics | 계측·대시보드·릴리즈 비교 |

### Definition of Done
- [ ] 각 에이전트 리포트에 Findings(파일:라인 근거) + Recommendations
- [ ] P0 병목 최소 1개 식별

---

## Phase 4: 개선안 설계 (Improvement Design)

### 산출물
- `.ai/plans/2026-02-13_perf_master_plan.md`

### 설계 기준: 체감속도 vs 실제성능 분리

| 패턴 타입 | 예시 | 측정 방법 |
|---|---|---|
| **체감속도** (Perceived) | 스켈레톤 UI, 프리페치, 낙관적 업데이트, 점진 렌더, 진행률 표시 | 사용자 관찰 + StatusMessage 갱신 시점 |
| **실제성능** (Actual) | COM 호출 최적화, 알고리즘 개선, 메모리 최적화, 병렬화, 빌드 최적화 | Stopwatch 전후 비교 |

### Definition of Done
- [ ] P0/P1/P2 우선순위 + 근거
- [ ] 커밋 단위별 변경요지, 수용기준, 롤백 절차
- [ ] 성능 예산(Perf Budget) 초안

---

## Phase 5: Codex 구현 (Implementation)

### 규칙
- 1 Task = 1 Commit
- 커밋 전 필수: `dotnet build && dotnet test`
- 커밋 후 권장: Debug 모드 앱 실행 → 벤치 수치 확인

### 중단 기준
- 동일 Task에서 2회 연속 빌드/테스트 실패 → `git revert HEAD` + `.ai/reports/2026-02-13_codex_blockers.md`
- 성능 회귀 감지 (벤치 수치 악화 20% 이상) → revert

---

## Phase 6: 성능 회귀 검증 (Regression Check)

### 방법
```bash
# 빌드 + 테스트
dotnet build MailTriageAssistant/MailTriageAssistant.csproj
dotnet test MailTriageAssistant.Tests/

# Debug 앱 실행 후 Stopwatch 출력 확인 (수동)
# Release 빌드 크기 확인
dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x64 --self-contained
```

### Definition of Done
- [ ] 전체 테스트 Green
- [ ] 주요 지표 목표값 이내
- [ ] 성능 예산 초과 0건

---

## Phase 7: 정리/문서화 (Wrap-up)

### 산출물
- `.ai/reports/2026-02-13_codex_change_log.md` 완성
- 산출 지표 전후 비교 표

---

## Cross-Cutting: 커밋 규칙

```
[NN] perf: 한줄설명
[NN] perceived: 한줄설명
[NN] bench: 한줄설명
```

## Cross-Cutting: 보안 불변 규칙 (성능 최적화에서도 위반 금지)
1. 이메일 본문을 디스크/로그에 저장하지 않음
2. PII를 마스킹 없이 클립보드에 복사하지 않음
3. 외부 API 호출 금지
