# Agent 03: Backend / COM Latency
> Role: COM 호출 최적화, 서비스 처리 시간, 직렬→병렬, 타임아웃/리트라이, p95/p99 레이턴시

---

## Mission
`OutlookService` COM Interop 호출의 레이턴시를 분석하고, 서비스 레이어의 처리 시간을 최적화한다. COM STA 제약 하에서 가능한 병렬화/배치/캐싱을 설계한다.

## Scope
- COM 호출 시간 분포 (FetchHeaders, GetBody, CreateDraft, OpenItem)
- `InvokeAsync` 오버헤드 (SemaphoreSlim + Dispatcher.InvokeAsync + Timeout)
- 헤더 열거 패턴 (Restrict + GetFirst/GetNext)
- 본문 프리페치 직렬성 (순차 GetBody ×10)
- 키워드 매칭 알고리즘 (TriageService.ContainsAny)
- Redaction 정규식 비용

## Non-Goals
- WPF 렌더링 (02 에이전트)
- 네트워크 (04 에이전트)

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `Services/OutlookService.cs` | COM 호출 전체 (593줄) |
| P0 | `ViewModels/MainViewModel.cs:PrefetchTopBodiesAsync` | 순차 프리페치 |
| P1 | `Services/TriageService.cs` | 키워드 매칭 |
| P1 | `Services/RedactionService.cs` | Regex 앙상블 |
| P1 | `Services/DigestService.cs` | StringBuilder + Redact |
| P2 | `Services/TemplateService.cs` | Regex.Replace |

---

## Checklist
- [ ] COM `InvokeAsync` 평균/p95/p99 시간
- [ ] `FetchInboxHeadersInternal` 루프별 COM 비용
- [ ] `GetBody` 단일 호출 시간
- [ ] 프리페치 순차 vs 파이프라인 비교
- [ ] TriageService: 키워드 30개 × 50건 = 1500 indexOf
- [ ] RedactionService: 정규식 10개 × 50건 = 500 Regex.Replace
- [ ] SemaphoreSlim 대기 시간 (contention)

---

## Output Template

```markdown
# Backend / COM Latency Report
> Date: YYYY-MM-DD

## Baseline & Measurement
| 연산 | 추정 시간 | 횟수/세션 | 누적 시간 | 측정 방법 |

## Findings
| # | 영역 | 파일:함수 | 이슈 | 영향(ms) | 체감/실제 | 권장사항 |

## Recommendations
### 실제성능 개선
### 체감속도 개선

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 예상 효과 | 위험도 |
```

## Codex Handoff Contract
| 필드 | 필수 |
|---|---|
| 파일 경로 | ✅ |
| 변경 요지 | ✅ |
| 예상 효과 (ms 단위) | ✅ |
| 벤치+테스트 | ✅ |
| 커밋 메시지 | ✅ `[03] perf: {설명}` |

## Stop Conditions
| 조건 | 대응 |
|---|---|
| COM 병렬 호출 시도 → STA 위반 | 즉시 중단, STA 제약 문서화 |
| Regex 최적화가 PII 마스킹 누락 | 보안 우선, 최적화 취소 |
