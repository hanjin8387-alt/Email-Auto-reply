# Agent 01: Profiling & Benchmark
> Role: 베이스라인 지표 정의, 측정 커맨드 확정, 프로파일링 포인트 제안

---

## Mission
`MailTriageAssistant`의 성능 베이스라인을 수치로 정의하고, 재현 가능한 측정 방법을 확정한다. 개선 전/후 비교를 위한 기반을 제공한다.

## Scope
- 핵심 지표 정의 (startup, header load, body load, digest, memory, publish size)
- 측정 코드 위치 식별 + 측정 커맨드 확정
- `PerfEventSource` ETW 활용도 평가
- `#if DEBUG` Stopwatch 삽입 포인트 감사
- Publish 바이너리 크기 베이스라인

## Non-Goals
- 코드 수정 (Codex 영역)
- 외부 프로파일러(dotTrace, PerfView) 설치 강제

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `Services/OutlookService.cs` | COM 호출 시간 측정 포인트 |
| P0 | `ViewModels/MainViewModel.cs` | 워크플로우별 Stopwatch 위치 |
| P0 | `Helpers/PerfEventSource.cs` | ETW EventSource 활용도 |
| P1 | `App.xaml.cs` | 앱 시작 시간 측정 포인트 |
| P1 | `MailTriageAssistant.csproj` | 빌드/Publish 설정 |
| P2 | `Services/*.cs` (전체) | 서비스별 처리 시간 |

---

## Checklist

### 정량 지표
- [ ] `startup_ms` — 앱 시작 → MainWindow.Loaded
- [ ] `header_load_ms` — FetchInboxHeaders 전체
- [ ] `body_load_ms` — GetBody 단일 호출
- [ ] `digest_ms` — GenerateDigestAsync 전체
- [ ] `prefetch_ms` — PrefetchTopBodiesAsync 전체
- [ ] `memory_mb` — Process.WorkingSet64
- [ ] `publish_size_mb` — Release 단일 파일 크기
- [ ] `com_call_avg_ms` — InvokeAsync 평균

### 측정 인프라
- [ ] `#if DEBUG` Stopwatch 누락 포인트 식별
- [ ] `PerfEventSource.Measure()` 호출 존재 여부
- [ ] 로그 출력 형식 표준화

---

## Output Template

```markdown
# Profiling & Benchmark Report
> Date: YYYY-MM-DD

## Baseline Metrics
| 지표 | 현재(추정) | 목표 | 측정 방법 | 코드 위치 |

## Measurement Infrastructure
| 계측 포인트 | 상태 | 파일:줄 |

## Findings
### 🔴 Critical
| # | 파일 | 이슈 | 권장사항 |
### 🟡 Major / 🟢 Minor ...

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치 커맨드 | 수용 기준 | 위험도 |
```

---

## Codex Handoff Contract
| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | 계측 코드 추가 위치 |
| 변경 요지 | ✅ | 어떤 지표를 어디서 측정 |
| 벤치 커맨드 | ✅ | `dotnet build -c Debug` → 앱 실행 → 로그 확인 |
| 수용 기준 | ✅ | 측정 값 출력 확인 |
| 커밋 메시지 | ✅ | `[01] bench: {설명}` |

## Stop Conditions
| 조건 | 대응 |
|---|---|
| ETW 수집 불가 | Stopwatch + ILogger 대안 사용 |
| 측정 코드가 Release 빌드에 영향 | `#if DEBUG` 게이트 필수 |
