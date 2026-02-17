# Agent 08: Observability / Metrics
> Role: 성능 지표 수집, 이벤트 스키마, 릴리즈 비교, 대시보드 설계

---

## Mission
성능 관련 지표 수집·기록·비교 체계를 설계한다. `PerfEventSource`, Serilog, `#if DEBUG` Stopwatch를 통합하여 릴리즈 간 성능 비교가 가능한 계측 프레임워크를 제안한다.

## Scope
- `PerfEventSource` 활용도 감사 + 확장 제안
- Serilog 구조화 로그에 성능 데이터 포함
- 이벤트 스키마 (이벤트명, 페이로드, PII 필터)
- 릴리즈 간 지표 비교 방법
- `Process` 메트릭 (WorkingSet, GC)

## Non-Goals
- 외부 APM (Application Insights, Datadog 등)
- 서버 사이드 메트릭

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `Helpers/PerfEventSource.cs` | ETW EventSource 현황 |
| P0 | `App.xaml.cs` | Serilog 구성 |
| P0 | `OutlookService.cs:#if DEBUG` | 계측 포인트 |
| P0 | `MainViewModel.cs:#if DEBUG` | 계측 포인트 |
| P1 | `Services/*.cs` | 서비스별 계측 현황 |

---

## Checklist
- [ ] `PerfEventSource.Measure()` 호출 위치 전수 확인
- [ ] `#if DEBUG` Stopwatch 위치 전수 확인
- [ ] Serilog에 `{ElapsedMs}` 포함 로그 존재 여부
- [ ] 릴리즈 간 비교용 지표 파일 출력 (JSON/CSV)
- [ ] 이벤트 스키마에 PII 미포함 확인

---

## Output Template

```markdown
# Observability / Metrics Report
> Date: YYYY-MM-DD

## Current Instrumentation
| 계측 방법 | 위치 | 상태 |

## Findings
| # | 영역 | 파일 | 이슈 | 권장사항 |

## Proposed Event Schema
| 이벤트명 | 트리거 | 페이로드 |

## Proposed Metrics
| 메트릭명 | 타입 | 수집 위치 |

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 위험도 |
```

## Stop Conditions
| 조건 | 대응 |
|---|---|
| 계측 코드가 Release 빌드에 성능 영향 | `#if DEBUG` 게이트 필수 |
| PII가 계측 데이터에 포함 | 즉시 제거 |
