# Agent 04: Network / Cache / Data
> Role: 캐시 전략, 프리페치 설계, 배치 최적화, 중복 호출 제거, 데이터 파이프라인

---

## Mission
COM → 서비스 → ViewModel 데이터 파이프라인의 캐싱/프리페치/배치 전략을 분석한다. 중복 COM 호출을 제거하고 데이터 재사용을 최대화한다.

## Scope
- 인메모리 캐시 전략 (헤더/본문)
- 프리페치 범위/타이밍 (현재 Top 10)
- 자동 분류 시 캐시 무효화
- 중복 GetBody 호출 방지
- 데이터 직렬 → 배치 변환 가능성
- 필터 변경 시 데이터 재활용

## Non-Goals
- HTTP/CDN 캐시 (WPF 데스크톱 앱이라 해당 없음)
- 오프라인 모드 (COM 의존이라 해당 없음)

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `MainViewModel.cs:PrefetchTopBodiesAsync` | 프리페치 전략 |
| P0 | `MainViewModel.cs:GenerateDigestAsync` | 중복 GetBody 호출 |
| P0 | `MainViewModel.cs:LoadSelectedEmailBodyAsync` | 선택 시 로딩 |
| P1 | `OutlookService.cs` | COM 호출 캐싱 가능성 |
| P1 | `Models/AnalyzedItem.cs:IsBodyLoaded` | 캐시 상태 플래그 |

---

## Checklist
- [ ] `IsBodyLoaded`로 중복 GetBody 방지 확인
- [ ] 프리페치 Top 10 → Digest Top 10과 동일한가
- [ ] 자동 분류에서 `Emails.Clear()` 후 캐시 모두 손실
- [ ] 카테고리 필터 변경 시 불필요한 재로드 없음 확인
- [ ] 항목 선택 → 프리페치 완료 항목이면 즉시 표시

---

## Output Template

```markdown
# Network / Cache / Data Report
> Date: YYYY-MM-DD

## Baseline & Measurement
| 연산 | 캐시 히트율 | 추정 절감 시간 |

## Findings
| # | 영역 | 파일 | 이슈 | 영향 | 권장사항 |

## Recommendations
### 캐시 전략
### 프리페치 최적화
### 중복 제거

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 위험도 |
```

## Stop Conditions
| 조건 | 대응 |
|---|---|
| 캐시가 메모리 과다 점유 | 캐시 크기 제한(LRU) 적용 |
| 캐시 데이터 stale → 잘못된 표시 | TTL 또는 명시적 무효화 |
