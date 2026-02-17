# Perceived Speed / UX Report — MailTriageAssistant
> Date: 2026-02-13

## Baseline & Measurement

| 시나리오 | 현재 대기 시간(추정) | 사용자 피드백 시점 | 갭(ms) | 비고 |
|---|---|---|---|---|
| 앱 시작 → Window 표시 | 1500-3000ms | Window Loaded 시점 | 0 | Window 표시 전 빈 화면 |
| "메일 분류 실행" 클릭 → 목록 표시 | 800-1500ms | ProgressBar 즉시 + StatusMessage | ~0 | ✅ 양호 |
| 이메일 선택 → 본문 표시 | 100-250ms | StatusMessage "본문 로딩 중" | ~0 | 짧지만 로딩 UI 없음 |
| Digest 클릭 → 완료 | 200-3000ms | ProgressBar + StatusMessage | ~0 | 미프리페치 시 느림 |
| 자동 분류 → 목록 갱신 | 800-1500ms | UI 깜빡임(Clear+AddRange) | **깜빡임** | ❌ 문제 |

---

## Findings

| # | 시나리오 | 파일 | 이슈 | 체감 영향 | 권장사항 |
|---|---|---|---|---|---|
| PS-01 | 앱 시작 | `App.xaml.cs`, `MainWindow.xaml` | 앱 시작 시 **빈 윈도우 → DI 완료 → 빈 목록** 순서. 사용자에게 "앱이 느리다" 인상 | ⬛⬛⬛⬛⬜ (High) | **스플래시 스크린** 또는 `MainWindow.Loaded` 전 스켈레톤 상태 표시 |
| PS-02 | 목록 갱신 | `MainViewModel.cs:230,262` | `Emails.Clear()` → `AddRange()` → 목록 깜빡임. 자동 분류 시 반복 | ⬛⬛⬛⬜⬜ (Medium) | 차분 업데이트 (NC-01과 동일) 또는 ListBox Opacity 애니메이션 |
| PS-03 | 본문 로딩 | `MainWindow.xaml` | 본문 영역에 로딩 인디케이터 없음. 100-250ms 동안 이전 본문 표시 → 갑자기 전환 | ⬛⬛⬜⬜⬜ (Low) | 우측 패널에 `IsBodyLoading` 바인딩 오버레이 |
| PS-04 | 프리페치 | `MainViewModel.cs:268,530` | 프리페치 진행 상태 없음. fire-and-forget. 프리페치 완료된 항목도 시각적 구분 없음 | ⬛⬛⬜⬜⬜ (Low) | StatusMessage에 "본문 프리페치 중 (N/10)" 표시 |
| PS-05 | 자동 분류 | `MainViewModel.cs:466` | `"다음 분류: Nm 후"` 텍스트만 표시. 카운트다운 없음 (정적 텍스트) | ⬛⬜⬜⬜⬜ (Low) | `DispatcherTimer`로 1분마다 텍스트 갱신 (카운트다운) |
| PS-06 | 선택 초기화 | `MainViewModel.cs:231` | 재로드 시 `SelectedEmail = null` → 우측 본문 영역 비워짐 → Empty State 표시 | ⬛⬛⬛⬜⬜ (Medium) | 이전 선택 `EntryId` 기억 + 재로드 후 복원 |
| PS-07 | 에러/재시도 | `MainViewModel.cs` 전체 | 에러 시 DialogService로 알림. **자동 재시도 없음**, StatusMessage만 변경. 사용자가 수동 재클릭 필요 | ⬛⬛⬜⬜⬜ (Low) | 자동 재시도는 자동 분류에서만 구현(3회). 수동은 현 상태 유지 |

---

## Recommendations

### 스켈레톤 / 플레이스홀더
1. **PS-01**: 앱 시작 시 `SplashScreen` 또는 `MainWindow` 스켈레톤 상태 (회색 블록) 표시
2. **PS-03**: 본문 영역 로딩 오버레이 (AnimatedBorder or Opacity)

### 점진적 렌더링
3. **PS-02**: 차분 업데이트로 깜빡임 제거

### 즉각 피드백
4. **PS-04**: 프리페치 진행률 StatusMessage
5. **PS-05**: 자동 분류 카운트다운 (매 분 갱신)
6. **PS-06**: 재로드 후 이전 선택 복원

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 예상 효과 | 위험도 |
|---|---|---|---|---|---|---|
| T-01 | `MainWindow.xaml` | 본문 영역 로딩 오버레이 (`IsLoading` 시 Grid 오버레이) | `dotnet build` | 빌드 성공 + 시각 확인 | 체감속도 | Low |
| T-02 | `MainViewModel.cs` | 프리페치 진행률 StatusMessage ("본문 프리페치 중 N/10") | `dotnet build` | 빌드 성공 | 체감속도 | Low |
| T-03 | `MainViewModel.cs` | 재로드 후 이전 `SelectedEmail.EntryId` 복원 | `dotnet build && dotnet test` | 빌드+테스트 통과 | 체감속도 | Low |
| T-04 | `MainViewModel.cs:UpdateAutoRefreshStatusText` | 카운트다운 갱신 (1분마다 "다음 분류: Nm Ns 후") | `dotnet build` | 빌드 성공 | 체감속도 | Low |
| T-05 | `App.xaml.cs` 또는 `.csproj` | SplashScreen 이미지 + `SplashScreen` 속성 | `dotnet build` | 빌드 성공 + 스플래시 표시 | 체감속도 | Medium |
