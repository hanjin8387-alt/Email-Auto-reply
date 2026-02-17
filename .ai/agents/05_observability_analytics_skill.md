# Agent 05: Observability & Analytics
> Role: 로깅·메트릭·트레이싱·이벤트 스키마·퍼널·가시성(디버깅) 강화

---

## Mission
`MailTriageAssistant`의 관측 가능성(Observability) 현황을 분석하고, 로깅·메트릭·이벤트 스키마를 통해 디버깅·운영·분석 역량을 강화한다.

## Scope
- 로깅 프레임워크 활용도 (Serilog 구성 + 실제 사용)
- 로그 포인트 적절성 (어디서 무엇을 로깅하는지)
- PII 필터링 (본문·이메일 로깅 금지)
- EventSource / ETW 활용 (`PerfEventSource.cs`)
- 메트릭 (처리 건수, 소요 시간, 에러율)
- 에러 리포팅 (Unhandled Exception, COM 에러)
- 사용자 행동 분석 (어떤 기능을 몇 번 사용했는지)

## Non-Goals
- 코드 구현
- 외부 모니터링 서비스 연동 (Application Insights 등)

---

## Inputs (우선순위)

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `App.xaml.cs` | 로깅 구성, 글로벌 에러 핸들러 |
| P0 | `Helpers/PerfEventSource.cs` | ETW EventSource 패턴 |
| P0 | `Services/OutlookService.cs` | COM 호출 시간 측정 포인트 |
| P1 | `ViewModels/MainViewModel.cs` | 사용자 행동 이벤트 포인트 |
| P1 | `MailTriageAssistant.csproj` | Serilog 패키지 |
| P2 | 모든 `Services/*.cs` | 로그 포인트 누락 여부 |

---

## Checklist

### 로깅
- [ ] Serilog 구성 확인 (Sink, Level, Format)
- [ ] 서비스별 `ILogger<T>` 주입 여부
- [ ] 로그 포인트: 서비스 호출 시작/끝/에러
- [ ] PII 필터링: 본문, 이메일 주소 미로깅 확인
- [ ] 구조화 로깅 (Structured Logging) 사용 여부

### 메트릭
- [ ] 처리 건수 (이메일 로드, 분류, Digest 생성)
- [ ] 소요 시간 (헤더 로드, 본문 로드, Digest)
- [ ] 에러 카운트 (COM, Timeout, 기타)

### 이벤트
- [ ] EventSource 이벤트 정의 확인
- [ ] 사용자 행동 이벤트 (버튼 클릭, 기능 사용)
- [ ] 세션 통계 (처리 건수, 카테고리 분포)

---

## Output Template

```markdown
# Observability & Analytics Report — MailTriageAssistant
> Date: YYYY-MM-DD

## Summary
- 로깅 포인트 현황: 구성됨/미활용
- 메트릭 수집: 없음/부분/완전
- 이벤트 스키마: 기초/미정의

## Current State
(현재 로깅/메트릭/이벤트 구현 현황)

## Findings
### 🔴 Critical
| # | 영역 | 파일 | 이슈 | 권장사항 |

### 🟡 Major
| # | 영역 | 파일 | 이슈 | 권장사항 |

### 🟢 Minor
| # | 영역 | 파일 | 이슈 | 권장사항 |

## Proposed Event Schema
| 이벤트명 | 트리거 | 페이로드 (PII 제외) | 용도 |
|---|---|---|---|

## Proposed Metrics
| 메트릭명 | 타입 | 수집 위치 | 목표 |
|---|---|---|---|

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
```

---

## Codex Handoff Contract

| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | 로깅/메트릭 추가 대상 |
| 로그 레벨 | ✅ | Information / Warning / Error |
| PII 안전성 | ✅ | 로그 내용에 PII 포함 여부 확인 |
| 테스트 커맨드 | ✅ | `dotnet build` |
| 커밋 메시지 | ✅ | `[05] observability: {설명}` |

---

## Stop Conditions

| 조건 | 대응 |
|---|---|
| 로그에 이메일 본문 포함 발견 | 즉시 보고 + 해당 로그 제거 |
| Serilog 구성 오류로 앱 시작 실패 | 로깅 코드를 try-catch로 감싸기 |
| PerfEventSource가 프로덕션 오버헤드 | #if DEBUG 게이트 적용 |
