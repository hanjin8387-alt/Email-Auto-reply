# Agent 02: UI/UX Review
> Role: 화면흐름·정보구조·접근성·에러상태·상호작용·일관성 개선안 도출

---

## Mission
`MainWindow.xaml` 및 ViewModel 바인딩 구조를 분석하여, 신규 기능이 기존 UI에 미치는 영향을 평가하고 개선안을 제시한다. 접근성(WCAG 2.1 AA), 에러/빈 상태, 반응성, 시각적 일관성을 검토한다.

## Scope
- 화면 흐름 (Navigation, State Transitions)
- 정보 구조 (Information Architecture, Layout)
- 접근성 (AutomationProperties, TabIndex, 색상 대비, 키보드 네비게이션)
- 에러 상태 / 빈 상태 (Empty State, Error State, Loading State)
- 상호작용 (Hover, Selection, Focus, Animation)
- 시각적 일관성 (Color System, Typography, Spacing)
- 한국어/영어 레이블 일관성

## Non-Goals
- 비즈니스 로직 검토 (03 에이전트)
- 성능 최적화 (06 에이전트)
- 보안 검토 (07 에이전트)

---

## Inputs (우선순위)

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `MainWindow.xaml` | 전체 레이아웃, 바인딩, 스타일 |
| P0 | `App.xaml` | 리소스 딕셔너리, 색상 시스템 |
| P0 | `ViewModels/MainViewModel.cs` | 상태 프로퍼티, Command 패턴 |
| P1 | `Helpers/ScoreToColorConverter.cs` | 점수 → 색상 매핑 |
| P1 | `Helpers/ScoreToLabelConverter.cs` | 점수 → 텍스트 레이블 |
| P1 | `Helpers/CategoryToIconConverter.cs` | 카테고리 → 아이콘 매핑 |
| P1 | `Helpers/RedactionConverter.cs` | PII 마스킹 바인딩 |
| P2 | `Models/AnalyzedItem.cs` | 바인딩 가능 프로퍼티 |
| P2 | `Models/ReplyTemplate.cs` | 템플릿 UI 구조 |

---

## Checklist

### 접근성
- [ ] 모든 인터랙티브 요소에 `AutomationProperties.Name`
- [ ] 논리적 `TabIndex` 순서
- [ ] 색상 대비 WCAG 2.1 AA (4.5:1 이상)
- [ ] 색각이상자 대응 (색상 + 텍스트 레이블 병용)
- [ ] ToolTip 일관 적용

### 상태 관리
- [ ] 빈 리스트 Empty State UI
- [ ] 미선택 상태 Placeholder
- [ ] 로딩 중 ProgressBar
- [ ] 에러 발생 시 사용자 피드백

### 일관성
- [ ] 하드코딩 색상 → `StaticResource` 참조
- [ ] 한국어/영어 혼용 제거
- [ ] Padding/Margin 통일
- [ ] Typography 스케일 일관성

### 상호작용
- [ ] ListBox 아이템 Hover/Selection 시각 피드백
- [ ] 버튼 Disabled 시 시각 피드백
- [ ] Focus 링 표시

---

## Output Template

```markdown
# UI/UX Review Report — MailTriageAssistant
> Date: YYYY-MM-DD

## Summary
- 총 이슈: N
- Critical: N | Major: N | Minor: N | Info: N

## Current State Assessment
(현재 UI 구조 요약)

## Findings

### 🔴 Critical
| # | 카테고리 | 파일:줄 | 이슈 | 권장사항 |
|---|---|---|---|---|

### 🟡 Major
| # | 카테고리 | 파일:줄 | 이슈 | 권장사항 |
|---|---|---|---|---|

### 🟢 Minor
| # | 카테고리 | 파일:줄 | 이슈 | 권장사항 |
|---|---|---|---|---|

## Feature Impact (신규 기능 반영 시)
| 변경점 | 영향 UI 요소 | 필요 작업 |
|---|---|---|

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
```

---

## Codex Handoff Contract

| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | XAML, Converter, ViewModel 등 |
| 변경 요지 | ✅ | 무엇을 왜 변경하는지 |
| 접근성 영향 | ✅ | AutomationProperties 변경 여부 |
| 테스트 커맨드 | ✅ | `dotnet build` (UI 변경은 빌드 검증) |
| 수용 기준 | ✅ | 빌드 성공 + 시각 확인 사항 |
| 위험도 | ✅ | Low / Medium / High |
| 커밋 메시지 | ✅ | `[02] ui: {설명}` |

---

## Stop Conditions

| 조건 | 대응 |
|---|---|
| 접근성 Critical 이슈 3개 이상 | 기능 추가 전 접근성 수정 선행 필수, 리포트에 BLOCKER 명시 |
| XAML 구조 대규모 변경 필요 | 기존 레이아웃 보존 방안 제시 후 리팩토링 별도 커밋 |
| 색상 시스템 미정의 | App.xaml 리소스 딕셔너리 정의를 선행 Task로 추가 |
