# Frontend Rendering Report (WPF) — MailTriageAssistant
> Date: 2026-02-13

## Baseline & Measurement

| 지표 | 값 | 측정 방법 |
|---|---|---|
| ListBox 항목 수 | 최대 50 | `MaxFetchCount` |
| DataTemplate 중첩 레벨 | 3 (Grid → StackPanel → TextBlock) | XAML 분석 |
| Converter 호출/렌더 | ~200회 (50건 × 4 Converter) | RedactionConverter(×2), ScoreToColor, ScoreToLabel, CategoryToIcon |
| `EmailsView.Refresh()` 호출/세션 | 3-5회 | 코드 분석 |
| 가상화 | ✅ 활성화 | XAML 분석 |

---

## Findings

| # | 카테고리 | 파일:줄 | 이슈 | 영향(추정) | 체감/실제 | 권장사항 |
|---|---|---|---|---|---|---|
| FR-01 | 갱신 | `MainViewModel.cs:230,262-263` | `Emails.Clear()` 후 `Emails.AddRange()` → UI 전체 리셋 후 재렌더. 50건이라 빠르지만 **깜빡임** 발생 가능 | 체감 | 체감 | 자동 분류 시 차분(diff) 업데이트 또는 `ListBox.Opacity` 0.3 → 1 애니메이션 |
| FR-02 | Converter | `MainWindow.xaml` RedactionConverter | `RedactionConverter.Convert()`가 매 렌더마다 `IRedactionService.Redact()` 호출. Sender+Subject × 50건 = 100회 Regex 실행. **바인딩 타겟이 변경되지 않아도** 가상화 재활용 시 재호출 | ~50-100ms | 실제 | (a) Redact 결과를 `AnalyzedItem`에 캐싱 (`RedactedSender`, `RedactedSubject` 프로퍼티), Converter 제거 |
| FR-03 | Refresh | `MainViewModel.cs:263,503,557,606` | `EmailsView.Refresh()`가 4곳에서 호출. CollectionView 전체 재평가 (Filter 50건) | ~5-10ms/call | 실제 | 필요 시만 호출. `PrefetchTopBodiesAsync` 종료 후 Refresh는 이미 존재 → 중복 체크 |
| FR-04 | 가상화 | `MainWindow.xaml` | `VirtualizingStackPanel.IsVirtualizing=True`, `VirtualizationMode=Recycling` ✅. 50건이라 사실상 전부 뷰포트 내이면 가상화 효과 제한 | 무시 | — | 현 상태 유지 |
| FR-05 | ProgressBar | `MainWindow.xaml:ProgressBar` | 상단 ProgressBar만 존재. **본문 로딩 시 우측 패널에 로딩 표시 없음** | 체감 | 체감 | 우측 본문 영역에 `Visibility=IsLoading` 오버레이 추가 |
| FR-06 | 깜빡임 | `MainViewModel.cs:230` | `Emails.Clear()` 시 `SelectedEmail = null` → 우측 본문 영역 비워짐 → 재로드 후 선택 없음. 자동 분류마다 선택 초기화 | 체감 | 체감 | 이전 선택 `EntryId` 기억 → 재로드 후 동일 항목 재선택 |

---

## Recommendations

### 체감속도 개선
1. **FR-01**: 자동 분류 시 목록 갱신 애니메이션 (Opacity fade)
2. **FR-05**: 본문 영역 로딩 오버레이/스켈레톤
3. **FR-06**: 재로드 후 이전 선택 항목 복원

### 실제성능 개선
1. **FR-02**: `RedactionConverter` 제거 → `AnalyzedItem.RedactedSender`, `RedactedSubject` 프로퍼티에 사전 계산값 저장. Converter 없이 직접 바인딩 → Regex 호출 100회 제거
2. **FR-03**: `EmailsView.Refresh()` 호출 최소화 — 중복 호출 제거

---

## Risk & Rollback
| 리스크 | 대응 |
|---|---|
| RedactedSender 사전 계산 → 모델 변경 | AnalyzedItem은 내부 모델이라 Breaking Change 없음 |
| Converter 제거 시 XAML 바인딩 변경 | 기존 Converter 유지하되 우선 프로퍼티 바인딩으로 전환 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 예상 효과 | 위험도 |
|---|---|---|---|---|---|---|
| T-01 | `Models/AnalyzedItem.cs`, `MainViewModel.cs` | `RedactedSender`, `RedactedSubject` 프로퍼티 추가 + LoadEmails에서 사전 계산 | `dotnet build && dotnet test` | 빌드+테스트 통과 | ~50-100ms 절감/렌더 | Medium |
| T-02 | `MainWindow.xaml` | Sender/Subject 바인딩을 `RedactedSender`/`RedactedSubject`로 변경, `RedactionConverter` 사용 제거 | `dotnet build` | 빌드 성공 + Sender 마스킹 유지 | Converter 호출 100회 제거 | Medium |
| T-03 | `MainViewModel.cs:LoadEmailsAsync` | 재로드 후 이전 `SelectedEmail.EntryId` 복원 | `dotnet build && dotnet test` | 빌드+테스트 통과 | 체감속도 | Low |
| T-04 | `MainWindow.xaml` | 본문 영역 로딩 오버레이 (IsLoading 바인딩) | `dotnet build` | 빌드 성공 | 체감속도 | Low |
| T-05 | `MainViewModel.cs` | `EmailsView.Refresh()` 중복 호출 제거 (503줄 제거 검토) | `dotnet build && dotnet test` | 빌드+테스트 통과 | ~5ms/call 절감 | Low |
