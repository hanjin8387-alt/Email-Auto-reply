# Agent 02: UI/UX Review

## Mission
WPF 대시보드의 사용성, 접근성, 시각 디자인, 반응형 레이아웃을 점검하고 개선 방안을 도출한다.

## Scope
- `MainWindow.xaml` (185줄) — 메인 레이아웃, 바인딩
- `MainWindow.xaml.cs` — 코드비하인드
- `Helpers/ScoreToColorConverter.cs` — 점수 시각화
- `ViewModels/MainViewModel.cs` — UI 동작 로직 (IsLoading, StatusMessage)

## Non-Goals
- 백엔드 로직 검토 (→ `01_code_review`)
- 성능 측정 (→ `05_perf_reliability`)

---

## Inputs (우선순위 파일 목록)

| 우선순위 | 파일 | 확인 포인트 |
|---|---|---|
| P0 | `MainWindow.xaml` | 레이아웃 구조, 바인딩, 접근성(ARIA), 반응형 |
| P0 | `Helpers/ScoreToColorConverter.cs` | 색각 이상자 대응, 대비율, 의미 전달 |
| P1 | `ViewModels/MainViewModel.cs` | IsLoading UX, 에러 상태 처리, 빈 상태 안내 |
| P2 | `MainWindow.xaml.cs` | 코드비하인드 최소화 확인 |

---

## Review Checklist

### 레이아웃 & 반응형
- [ ] 창 크기 조절 시 컨텐츠 잘림 없음
- [ ] MinWidth/MinHeight 적절성 (현재 MinWidth=900)
- [ ] 2컬럼 비율(2:3) 적절성
- [ ] 긴 제목/발신자명 처리 (TextTrimming)

### 사용성 (Usability)
- [ ] 로딩 인디케이터 표시 (`ProgressBar` + `IsLoading`)
- [ ] 빈 리스트 상태 안내 메시지 존재 여부
- [ ] 에러 상태 시 사용자 안내 (Outlook 미연결, New Outlook 등)
- [ ] 이메일 선택 → 본문 로딩 피드백
- [ ] 버튼 상태 (비활성화 시 시각적 표시)

### 접근성 (Accessibility)
- [ ] 키보드 탐색 가능 (Tab 순서)
- [ ] 스크린 리더 호환 (`AutomationProperties.Name`)
- [ ] 고대비 모드 지원 여부
- [ ] 폰트 크기 확대 시 깨짐 여부

### 시각 디자인
- [ ] 색상 일관성 (하드코딩된 hex vs 리소스)
- [ ] 점수 색상 의미 전달 (빨강=긴급, 회색=낮음)
- [ ] 카테고리 시각화 (아이콘 or 뱃지)
- [ ] 여백/패딩 일관성

### 한국어 UX
- [ ] 한글 텍스트 잘림 없음
- [ ] 한글 폰트 가독성 (기본 폰트 적절성)
- [ ] 한국어 버튼 레이블 직관성

---

## Output Template

산출물 경로: `.ai/reports/YYYY-MM-DD_uiux.md`

```markdown
# UI/UX Review Report — MailTriageAssistant
> Date: YYYY-MM-DD
> Reviewer: Agent 02 (UI/UX)

## Summary
- Total Issues: N
- Critical: N | Major: N | Minor: N | Info: N

## Current State Assessment
(스크린샷 기반 현재 상태 설명)

## Findings

### 🔴 Critical
| # | Area | Issue | Impact | Recommendation |
|---|---|---|---|---|
| C-1 | 접근성 | 설명 | 대상 사용자 | 수정안 |

### 🟡 Major
| # | Area | Issue | Impact | Recommendation |
|---|---|---|---|---|

### 🟢 Minor
| # | Area | Issue | Impact | Recommendation |
|---|---|---|---|---|

### ⚪ Info
| # | Area | Issue | Impact | Recommendation |
|---|---|---|---|---|

## Proposed Wireframe Changes
(레이아웃 변경 제안 시 텍스트 기반 와이어프레임)

## Codex Handoff
```

---

## Codex Handoff

1. **XAML 수정 작업 목록**
   - 각 항목: `XAML 요소`, `변경 내용`, `바인딩 영향`
   
2. **커밋 절차**
   ```
   1) MainWindow.xaml 수정
   2) dotnet build → XAML 파싱 성공 확인
   3) 앱 실행 → 시각적 확인
   4) 커밋: [02] ui: {한줄 설명}
   ```

3. **변경 시 주의사항**
   - 바인딩 경로 변경 시 ViewModel 프로퍼티도 함께 수정
   - 색상 변경 시 ScoreToColorConverter와 동기화
   - 접근성 속성 추가 시 스크린 리더 테스트
