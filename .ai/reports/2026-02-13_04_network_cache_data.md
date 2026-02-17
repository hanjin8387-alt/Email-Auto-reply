# Network / Cache / Data Report — MailTriageAssistant
> Date: 2026-02-13

## Baseline & Measurement

| 연산 | 캐시 히트율 | 추정 절감 시간 |
|---|---|---|
| 본문 로드 (선택 시) — `IsBodyLoaded` 체크 | ~50% (프리페치 Top 10 중 선택 시) | 100-250ms/건 |
| 프리페치 → Digest 중복 방지 | ~80% (프리페치 Top 10 = Digest Top 10) | 800-2000ms |
| 자동 분류 → `Emails.Clear()` → 캐시 전량 손실 | 0% 재활용 | 1000-2500ms 재프리페치 |

---

## Findings

| # | 영역 | 파일 | 이슈 | 영향 | 권장사항 |
|---|---|---|---|---|---|
| NC-01 | 캐시 손실 | `MainViewModel.cs:230` | `Emails.Clear()` → **모든 AnalyzedItem(본문 포함) 폐기**. 자동 분류(10분 간격)마다 프리페치 1-2.5s 재소비 | 1000-2500ms | 차분 업데이트: 기존 EntryId와 비교 → 신규/삭제만 처리, 기존 항목(IsBodyLoaded=true) 재활용 |
| NC-02 | 프리페치 범위 | `MainViewModel.cs:534` | 프리페치 대상은 `Emails.Take(10)` = Score+ReceivedTime 내림차순 상위. Digest 대상도 동일 정렬 Top 10. **일치 ✅** 이지만, 사용자가 스크롤로 11~50번 항목을 선택하면 미프리페치 | — | Viewport 기반 프리페치 (WPF ScrollChanged 이벤트) — 현재 규모에서 ROI 낮음 |
| NC-03 | 중복 GetBody | `MainViewModel:GenerateDigestAsync:587-590` | 프리페치 완료 전 Digest 클릭 시 동일 항목에 **중복 GetBody** 발생 가능. `IsBodyLoaded` 체크 적용됨 ✅ 하지만, 프리페치 진행 중인 항목은 아직 `IsBodyLoaded=false` → 동시 호출 | ~100-250ms × 경합건수 | 프리페치 `Task` 참조 보관 → Digest에서 `await prefetchTask` 후 시작 |
| NC-04 | 헤더 캐시 | `OutlookService:FetchInboxHeadersInternal` | 헤더는 매번 COM에서 재로드. 짧은 간격(수동 재클릭)에도 COM 풀 호출 | 800-1500ms | 헤더 캐시 TTL (예: 30초 이내 재호출 시 캐시 반환). 자동 분류 간격(분 단위)에서는 항상 새로고침 |

---

## Recommendations

### 캐시 전략

1. **NC-01 (P0)**: 차분 업데이트 — `LoadEmailsAsync`에서 기존 `Emails` EntryId 세트와 신규 헤더 EntryId 비교
   - 신규: 추가 + 분류
   - 삭제: 제거
   - 유지: 기존 AnalyzedItem 재사용 (IsBodyLoaded, RedactedSummary 보존)
   - **예상 효과**: 자동 분류 시 프리페치 건수 대폭 감소 (변경분만 프리페치)

2. **NC-04 (P2)**: 헤더 TTL 캐시 — `OutlookService`에 `_lastFetchTime` + `_cachedHeaders` → 30초 이내 재호출 시 캐시 반환

### 프리페치 최적화

3. **NC-03 (P1)**: Digest에서 프리페치 Task 완료 대기. `MainViewModel`에 `_prefetchTask` 필드 보관 → `GenerateDigestAsync` 진입 시 `await _prefetchTask`

### 중복 제거

4. **중복 GetBody**: 현재 `IsBodyLoaded` 체크로 대부분 방지됨 ✅. NC-03의 경합만 해결하면 충분.

---

## Risk & Rollback

| 리스크 | 대응 |
|---|---|
| 차분 업데이트가 정렬 순서 불일치 유발 | 차분 후 재정렬 적용 |
| 헤더 TTL 캐시가 stale 데이터 표시 | TTL 30초 + "새로고침" 버튼은 캐시 무시 |
| 프리페치 Task await가 Digest 시작 시간 증가 | 프리페치 미완료 시 `Task.WhenAny(prefetchTask, Task.Delay(500))` |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 예상 효과 | 위험도 |
|---|---|---|---|---|---|---|
| T-01 | `MainViewModel.cs:LoadEmailsAsync` | 차분 업데이트: 기존 EntryId와 비교, 신규만 추가/삭제, 기존 재활용 | `dotnet build && dotnet test` | 빌드+테스트 통과 + 자동 분류 시 캐시 유지 | 1-2.5s 프리페치 절감 | High |
| T-02 | `MainViewModel.cs` | `_prefetchTask` 필드 추가 → `GenerateDigestAsync`에서 await | `dotnet build && dotnet test` | 빌드+테스트 통과 | 중복 GetBody 제거 | Medium |
| T-03 | `OutlookService.cs` | 헤더 TTL 캐시 (30초) | `dotnet build && dotnet test` | 빌드+테스트 통과 | 짧은 간격 재호출 절감 | Medium |
