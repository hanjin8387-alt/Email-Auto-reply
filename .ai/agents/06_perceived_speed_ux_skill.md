# Agent 06: Perceived Speed / UX
> Role: 체감속도 개선 — 스켈레톤, 프리페치, 점진적 로딩, 즉각 피드백, 로딩 상태

---

## Mission
사용자가 "빠르다"고 느끼는 UX 패턴을 분석하고, 실제 연산 시간과 무관하게 **즉각적 피드백**을 통한 체감속도를 개선한다.

## Scope
- 로딩 인디케이터 적절성 (ProgressBar, StatusMessage)
- 스켈레톤 UI / 플레이스홀더 적용 가능성
- 점진적 렌더링 (한 번에 50건 vs 점진 표시)
- 프리페치 타이밍과 체감 효과
- 인터랙션 즉시성 (버튼 클릭 → 즉각 반응)
- 에러/재시도 UX (실패 시 자연스러운 복구)
- 로딩 상태 전환 (IsLoading 플래그 활용도)

## Non-Goals
- 실제 연산 속도 개선 (03 에이전트)
- COM 최적화

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `MainWindow.xaml` | ProgressBar, Empty State, 로딩 UI |
| P0 | `MainViewModel.cs` | IsLoading, StatusMessage, LoadEmailsAsync |
| P1 | `MainViewModel.cs:PrefetchTopBodiesAsync` | 프리페치 UX 효과 |
| P1 | `MainWindow.xaml:DataTemplate` | 목록 항목 렌더링 |
| P2 | `Models/AnalyzedItem.cs` | IsBodyLoaded 표시 |

---

## Checklist
- [ ] "메일 분류 실행" 클릭 → 즉각적 피드백 (0ms 얼마나 빨리 반응?)
- [ ] ProgressBar가 충분히 눈에 띄는가
- [ ] 본문 로딩 중 오버레이/스켈레톤 존재 여부
- [ ] 목록 갱신 시 깜빡임(Emails.Clear → AddRange) 발생 여부
- [ ] 프리페치 완료 시 "이미 로드됨" 시각적 표시 여부
- [ ] 자동 분류 진행 중 사용자 알림 적절성
- [ ] 에러 발생 시 재시도 UX
- [ ] Empty State 존재 + 적절한 안내 메시지

---

## Output Template

```markdown
# Perceived Speed / UX Report
> Date: YYYY-MM-DD

## Baseline & Measurement
| 시나리오 | 현재 대기 시간 | 사용자 피드백 시점 | 갭(ms) |

## Findings
| # | 시나리오 | 파일 | 이슈 | 체감 영향 | 권장사항 |

## Recommendations
### 스켈레톤/플레이스홀더
### 점진적 렌더링
### 즉각 피드백
### 에러/재시도

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 위험도 |
```

## Stop Conditions
| 조건 | 대응 |
|---|---|
| 스켈레톤이 오히려 혼란 (표시 후 급변) | 표시 임계값 300ms 적용 |
| 점진 렌더가 목록 깜빡임 유발 | RangeObservableCollection 유지 |
