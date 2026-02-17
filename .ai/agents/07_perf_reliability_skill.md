# Agent 07: Performance Reliability
> Role: 회귀 방지, 성능 예산(Perf Budget), 가드레일, 스모크 벤치, 메모리 릭

---

## Mission
성능 개선 후 회귀를 방지하는 메커니즘을 설계한다. Perf Budget 정의, CI 게이트, 메모리 릭 징후, 반복 실행 안정성을 분석한다.

## Scope
- 성능 예산(Perf Budget) 정의
- 벤치마크 스모크 테스트 설계
- 메모리 릭 징후 탐지 (COM 미해제, 이벤트 핸들러 누수, Dispatcher 누수)
- 반복 실행 안정성 (자동 분류 10회 연속)
- CI/빌드 가드레일 제안

## Non-Goals
- 부하 테스트 (단일 사용자 데스크톱 앱)
- 클라우드 스케일링

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `OutlookService.cs:SafeReleaseComObject` | COM 해제 패턴 |
| P0 | `OutlookService.cs:Dispose` | 리소스 해제 완전성 |
| P0 | `MainViewModel.cs` | DispatcherTimer, CancellationTokenSource 해제 |
| P1 | `ClipboardSecurityHelper.cs` | Timer 해제 |
| P1 | `Helpers/TaskExtensions.cs` | SafeFireAndForget 누수 가능성 |
| P2 | `.csproj` | CI 설정 |

---

## Checklist
- [ ] COM 객체 모두 SafeReleaseComObject 호출
- [ ] `IDisposable` 구현 클래스 전수 검사
- [ ] `CancellationTokenSource` Dispose 호출
- [ ] `DispatcherTimer` 비활성화 + 이벤트 해제
- [ ] `SafeFireAndForget` 예외 로깅 확인
- [ ] 메모리 반복 측정: 10회 LoadEmails 후 WorkingSet 증가량
- [ ] Perf Budget 초안 (startup ≤ 2s, header_load ≤ 1.5s, body ≤ 300ms)

---

## Output Template

```markdown
# Performance Reliability Report
> Date: YYYY-MM-DD

## Perf Budget (초안)
| 지표 | 상한 | 현재 | 여유 |

## Memory Analysis
| 시나리오 | 전 | 후 | 증가 | 상태 |

## Findings
| # | 영역 | 파일 | 이슈 | 영향 | 권장사항 |

## Regression Prevention
| 방법 | 구현 난이도 | 효과 |

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 위험도 |
```

## Stop Conditions
| 조건 | 대응 |
|---|---|
| 메모리 릭 확증 | 릭 수정 선행 → 최적화 후행 |
| COM 미해제 패턴 발견 | 즉시 수정 (안정성 우선) |
