# Backend / COM Latency Report — MailTriageAssistant
> Date: 2026-02-13

## Baseline & Measurement

| 연산 | 추정 시간 | 횟수/세션 | 누적 시간 | 측정 방법 |
|---|---|---|---|---|
| `InvokeAsync` 오버헤드 | 1-3ms | 매 COM 호출 | — | SemaphoreSlim + Dispatcher marshal |
| `FetchInboxHeadersInternal` (50건) | 800-1500ms | 1-6/세션 | 800-9000ms | `#if DEBUG` Stopwatch ✅ |
| `GetBodyInternal` (1건) | 100-250ms | 1-60/세션 | 100-15000ms | `#if DEBUG` Stopwatch ✅ |
| `PrefetchTopBodiesAsync` (10건 순차) | 1000-2500ms | 1-6/세션 | 1000-15000ms | **미측정** |
| `TriageService.AnalyzeHeader` (1건) | < 0.5ms | 50/로드 | < 25ms | 무시 |
| `TriageService.AnalyzeWithBody` (1건) | < 1ms | 10/프리페치 | < 10ms | 무시 |
| `RedactionService.Redact` (1개 본문) | 1-5ms (Regex 10개) | 10/프리페치 | 10-50ms | 미측정 |
| `DigestService.GenerateDigest` | 5-20ms (StringBuilder) | 1/세션 | 5-20ms | 미측정 |

---

## Findings

| # | 영역 | 파일:함수 | 이슈 | 영향(ms) | 체감/실제 | 권장사항 |
|---|---|---|---|---|---|---|
| BL-01 | COM 직렬 | `MainViewModel:PrefetchTopBodiesAsync:530-555` | 10건 본문을 **순차** GetBody. 각 100-250ms → 총 1-2.5s. COM STA 제약으로 진정한 병렬화 불가하지만, **파이프라인화**(요청/결과 인터리빙)도 미적용 | 1000-2500 | 실제 | COM STA 직렬 제약 인정. 대신 프리페치 **건수 조정**(10→5) 또는 **조기 중단**(사용자 선택 시) |
| BL-02 | Digest 경로 | `MainViewModel:GenerateDigestAsync:584-604` | Digest Top 10 중 미로드 본문을 **다시 순차 GetBody**. 프리페치와 중복될 수 있으나 보장 없음 | 0-2500 | 실제 | 프리페치 완료 대기 후 Digest 시작. 또는 프리페치 범위를 Digest 대상과 동기화 |
| BL-03 | COM 오버헤드 | `OutlookService:InvokeAsync:139-170` | `SemaphoreSlim.WaitAsync` + `Dispatcher.InvokeAsync` + `Task.WhenAny(task, timeout)` → 매 호출마다 3개 비동기 프레임워크 래핑. 직접 비용 ~1-3ms지만, **60+ 호출/세션 → 60-180ms** 순수 오버헤드 | 60-180 | 실제 | 오버헤드 자체는 안전성 비용. 감수 가능. 다만 배치 COM 호출(여러 GetBody를 1회 Invoke에 담기) 검토 |
| BL-04 | 배치 GetBody | `OutlookService` 없음 | `GetBody`를 개별 COM 호출로 처리. 10건이면 10회 `InvokeAsync`. **STA 직렬이라 병렬은 불가하지만, 1회 Invoke 안에서 10건 루프**는 가능 → `InvokeAsync` 오버헤드 9회 절감 + SemaphoreSlim 9회 대기 제거 | ~30-90 절감 | 실제 | `GetBodies(string[] entryIds)` 배치 메서드 추가. 내부에서 foreach loop |
| BL-05 | 키워드 매칭 | `TriageService.ContainsAny` | `string.IndexOf` 루프. ~30 키워드 × 50건 = 1500 호출. 현재 규모에서 ~25ms 이내 | < 25 | 실제 | .NET 8 `SearchValues<string>` 미지원 (byte/char만). `Regex` 사전 컴파일된 합짝패턴 고려. 현재 규모에서 변경 불필요 |
| BL-06 | Regex 앙상블 | `RedactionService.Redact` | 10개 정규식 순차 `Replace`. 각 ~0.5ms → 1건 ~5ms × 10건 = 50ms | ~50 | 실제 | `Regex.CompileToAssembly`는 .NET 8에서 미지원. `[GeneratedRegex]` source generator 적용 → JIT 비용 제거 + 약간의 속도 개선 |

---

## Recommendations

### 실제성능 개선

1. **BL-04**: `GetBodies(IReadOnlyList<string> entryIds)` 배치 메서드 → 1회 `InvokeAsync`에서 N건 루프 → N-1회 `SemaphoreSlim` + `Dispatcher` 오버헤드 절감
2. **BL-06**: `RedactionService` 정규식에 `[GeneratedRegex]` source generator 적용 (.NET 8)
3. **BL-01**: 프리페치 건수를 설정 가능하게 (`PrefetchCount` in `TriageSettings`)

### 체감속도 개선

1. **BL-02**: Digest 생성 전 프리페치 진행 중이면 완료 대기 또는 이미 로드된 항목만으로 Digest 생성
2. **BL-01**: 프리페치 중 사용자 알림 (StatusMessage 업데이트: "본문 프리페치 중 (3/10)")

---

## Risk & Rollback

| 리스크 | 대응 |
|---|---|
| 배치 GetBody 중 일부 실패 | per-item try-catch 유지 (현재 패턴) |
| `[GeneratedRegex]` 소스 생성기가 Regex 패턴 비호환 | 점진 적용: 1개씩 전환, 테스트 확인 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치+테스트 | 수용 기준 | 예상 효과 | 위험도 |
|---|---|---|---|---|---|---|
| T-01 | `IOutlookService.cs`, `OutlookService.cs` | `GetBodies(IReadOnlyList<string> entryIds)` 배치 메서드 추가. 내부 단일 InvokeAsync에서 foreach | `dotnet build && dotnet test` | 빌드+테스트 통과 | ~30-90ms/프리페치 | Medium |
| T-02 | `MainViewModel.cs:PrefetchTopBodiesAsync` | `GetBodies` 배치 메서드 호출로 전환 | `dotnet build && dotnet test` | 빌드+테스트 통과 | InvokeAsync 오버헤드 9회 절감 | Medium |
| T-03 | `MainViewModel.cs:GenerateDigestAsync` | 미로드 분문에 대해서도 `GetBodies` 배치 호출 | `dotnet build && dotnet test` | 빌드+테스트 통과 | 동일 | Low |
| T-04 | `RedactionService.cs` | 정규식 10개에 `[GeneratedRegex]` source generator 적용 | `dotnet build && dotnet test` | 빌드+테스트 통과 + PII 테스트 Green | Regex JIT 비용 제거 | Medium |
| T-05 | `Models/TriageSettings.cs`, `appsettings.json` | `PrefetchCount: int = 10` 설정 추가 | `dotnet build && dotnet test` | 빌드+테스트 통과 | 설정 가능 | Low |
| T-06 | `MainViewModel.cs:PrefetchTopBodiesAsync` | 프리페치 중 StatusMessage 갱신 ("본문 프리페치 중 (N/M)") | `dotnet build` | 빌드 성공 | 체감속도 | Low |
