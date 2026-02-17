# Agent 02: Frontend Rendering (WPF)
> Role: WPF 렌더 병목, 불필요 갱신, ListBox 가상화, 바인딩 효율, 메인스레드 부하

---

## Mission
WPF UI 레이어의 렌더링 성능을 분석한다. ListBox 가상화, DataTemplate 복잡도, 바인딩 갱신 빈도, ICollectionView.Refresh 비용, Dispatcher 부하를 평가한다.

## Scope
- ListBox 가상화 설정 (VirtualizingStackPanel)
- DataTemplate 복잡도 (중첩 수준, Converter 호출)
- `ObservableCollection` / `RangeObservableCollection` 갱신 패턴
- `ICollectionView.Refresh()` 호출 빈도/비용
- Converter 호출 빈도 (ScoreToColor, ScoreToLabel, CategoryToIcon, Redaction)
- Dispatcher 호출 빈도 (UI 스레드 블로킹)
- 애니메이션/Trigger 비용

## Non-Goals
- COM 최적화 (03 에이전트), 매크로 벤치마킹

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `MainWindow.xaml` | ListBox, DataTemplate, 가상화, Trigger |
| P0 | `App.xaml` | 리소스 딕셔너리, Style |
| P0 | `ViewModels/MainViewModel.cs` | `Emails.AddRange`, `EmailsView.Refresh()`, 프로퍼티 변경 빈도 |
| P1 | `Helpers/RangeObservableCollection.cs` | Batch 갱신 메커니즘 |
| P1 | `Helpers/ScoreToColorConverter.cs` | Converter 호출 빈도 |
| P1 | `Helpers/RedactionConverter.cs` | Converter 내 서비스 호출 |
| P2 | `Models/AnalyzedItem.cs` | INotifyPropertyChanged 빈도 |

---

## Checklist
- [ ] ListBox `VirtualizingStackPanel.IsVirtualizing` = True
- [ ] `VirtualizingStackPanel.VirtualizationMode` = Recycling
- [ ] `ScrollViewer.CanContentScroll` = True
- [ ] DataTemplate 중첩 레벨 ≤ 3
- [ ] `ICollectionView.Refresh()` 호출 최소화
- [ ] `RangeObservableCollection.AddRange()` — Reset 이벤트 1회
- [ ] Converter에서 무거운 연산 없음
- [ ] `RedactionConverter`가 Redact() 호출 — 50건 ×2(Sender+Subject) = 100회

---

## Output Template

```markdown
# Frontend Rendering Report (WPF)
> Date: YYYY-MM-DD

## Baseline & Measurement
| 지표 | 값 | 측정 방법 |

## Findings
| # | 카테고리 | 파일:줄 | 이슈 | 영향(추정) | 체감/실제 | 권장사항 |

## Recommendations (체감속도 vs 실제성능 분리)
### 체감속도 개선
### 실제성능 개선

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 예상 효과 | 위험도 |
```

## Codex Handoff Contract
| 필드 | 필수 |
|---|---|
| 파일 경로 | ✅ |
| 변경 요지 | ✅ |
| 체감/실제 분류 | ✅ |
| 벤치+테스트 커맨드 | ✅ |
| 커밋 메시지 | ✅ `[02] perf: {설명}` or `[02] perceived: {설명}` |

## Stop Conditions
| 조건 | 대응 |
|---|---|
| 가상화 변경이 기존 레이아웃 깨짐 | 변경 보류 + 대안 경로 |
| Converter 최적화가 보안 마스킹 약화 | 보안 우선 → 최적화 취소 |
