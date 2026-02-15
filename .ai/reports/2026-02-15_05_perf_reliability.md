# Performance & Reliability Report — MailTriageAssistant
> Date: 2026-02-15
> Reviewer: Agent 05 (Performance & Reliability)

## Summary
- Total Issues: **15**
- Critical: **4** | Major: **5** | Minor: **4** | Info: **2**

---

## Performance Baseline (추정)

| Metric | Current (Est.) | Target | Gap |
|---|---|---|---|
| 앱 시작 시간 | ~4–6s (STA 스레드 생성 + COM 바인딩) | <3s | **1–3s 초과** |
| 50 이메일 헤더 로드 | ~2000–4000ms (Items.Sort + 순차 인덱서 루프) | <1000ms | **1000–3000ms 초과** |
| 본문 1건 로드 | ~100–250ms | <200ms | 경계치 |
| Digest 생성 (10건) | ~200–400ms (미로드 시 순차 GetBody ×10 → ~2500ms) | <500ms | ⚠ 미로드 시 목표 초과 |

---

## Findings

### 🔴 Critical

| # | Area | File:Line | Issue | Impact | Recommendation |
|---|---|---|---|---|---|
| C-01 | COM Interop | `Services/OutlookService.cs:127-210` | `FetchInboxHeadersInternal()` 에서 전체 Inbox의 `items.Count` 조회 후 `Items.Sort("[ReceivedTime]", true)` → **전체 컬렉션 정렬**. 대량 Inbox (수천~수만 건) 시 Outlook COM이 전체 아이템을 로드·정렬하여 수 초 이상 소요 가능 | 헤더 로딩 ~2-4s, UI 스레드는 직접 block되지 않지만 UX 지연 체감 | `Items.Sort` 대신 `Items.Restrict("[ReceivedTime] >= '...'")` 로 최근 7일만 필터 후 정렬. 또는 MAPI `Table` 객체 사용 |
| C-02 | COM Interop | `Services/OutlookService.cs:145-181` | `for` 루프에서 **`items[i]` 인덱서 접근** — Outlook COM `Items` 컬렉션의 1-based 인덱서는 내부적으로 매번 선형 탐색할 수 있어 **O(n²)** 위험 | 대량 아이템 시 극심한 성능 저하(수천 개 Inbox에서 50개 추출 시 체감 ~5s+) | `Items.GetFirst()` + `Items.GetNext()` 순차 열거 패턴으로 변경 |
| C-03 | UI 응답성 | `ViewModels/MainViewModel.cs:218-281` | `GenerateDigestAsync()` 에서 미로드 본문을 **순차적으로** `await GetBody()` × 최대 10회 호출. 각 COM 호출 ~200ms면 총 ~2s. UI 스레드는 안 막히지만 `IsLoading` 동안 장시간 대기 | Digest 생성 ~2s+ 체감 지연, 사용자가 "멈춘 것 같다"고 느낌 | 방법 A: `LoadEmailsAsync()` 완료 직후 Top-10 본문 백그라운드 프리페치. 방법 B: STA 순차 특성상 실질적 병렬화 불가이므로 프리페치가 최선 |
| C-04 | 메모리/COM 누수 | `Services/OutlookService.cs:212-260` | `GetBodyInternal()` 에서 `mail.Body` 접근 시 Outlook 내부 COM 래퍼(Body 문자열용)가 생성될 수 있으나 별도 `ReleaseComObject` 미수행. `raw`만 finally에서 해제 — 동일 참조이므로 단건은 문제 없으나, 반복 호출 시 **COM RCW(Runtime Callable Wrapper) 누적** 가능 | 장시간 사용 시 Outlook 핸들 고갈, 메모리 증가 | `body = mail.Body` 후 코드 의도를 명확히 하고, `finally` 블록에서 `SafeReleaseComObject(raw)` 유지 + COM 접근 패턴 문서화. 반복 호출 시 `GC.Collect()` + `GC.WaitForPendingFinalizers()` 주기적 호출 검토 (주의: 성능 트레이드오프) |

### 🟡 Major

| # | Area | File:Line | Issue | Impact | Recommendation |
|---|---|---|---|---|---|
| M-01 | 에러 복구 | `Services/OutlookService.cs:63-69` | `EnsureClassicOutlookOrThrow()` 에서 **`Process.GetProcessesByName("olk")` 매번 호출** — 모든 COM 호출마다 프로세스 목록 전체 스캔. 또한 New Outlook이 설치만 된 경우(사용 안 함) false positive 가능 | 매 호출마다 ~5–20ms 오버헤드, 잘못된 차단 가능 | 앱 시작 시 1회만 검사하고 `_newOutlookChecked` 플래그로 캐싱. `ResetConnection()` 시 플래그 리셋 |
| M-02 | UI 응답성 | `ViewModels/MainViewModel.cs:113-173` | `LoadEmailsAsync()` 에서 `Emails.Add(item)` 를 **50회 개별 호출** → `ObservableCollection`이 매번 `CollectionChanged` 이벤트 발생 → 50회 UI 갱신 | 리스트 렌더링 플리커, FPS 저하 | `RangeObservableCollection<T>` 도입하여 `AddRange()` 1회로 일괄 추가. 또는 `ICollectionView.DeferRefresh()` 활용 |
| M-03 | 가상화 | `MainWindow.xaml:66-104` | `ListBox`에 `VirtualizingStackPanel.IsVirtualizing="True"` / `VirtualizationMode="Recycling"` **미설정**. 50개 항목의 DataTemplate이 전부 즉시 생성 | 초기 렌더링 오버헤드, 향후 확장 시 성능 병목 | `<ListBox VirtualizingStackPanel.IsVirtualizing="True" VirtualizingStackPanel.VirtualizationMode="Recycling" ScrollViewer.CanContentScroll="True">` 추가 |
| M-04 | 성능 | `Services/TriageService.cs:68-122` | `AnalyzeInternal()` 에서 `combined = subject + " " + body` 로 body 전체 (최대 1500자) 결합 후 6종 키워드 배열을 `string.IndexOf`로 순차 탐색. 50 이메일 × ~30 키워드 = ~1500번 `IndexOf` | 현재 규모에서 ~50ms 이내이나 확장 시 선형 증가 | 키워드 검색 대상을 body 첫 200자로 제한. 또는 `SearchValues<string>` (.NET 8+) 활용 |
| M-05 | 에러 복구 / 타임아웃 | `Services/OutlookService.cs:57-61` | `InvokeAsync<T>()` 에 **타임아웃 없음**. Outlook 무응답(hang) 시 `Dispatcher.InvokeAsync`가 영원히 대기 → 앱 전체 hang | 앱 완전 멈춤, 사용자 강제 종료 필요 | `Task.WhenAny(dispatcherTask, Task.Delay(15s))` 패턴으로 타임아웃 적용. 타임아웃 시 `ResetConnection()` + 사용자 안내 |

### 🟢 Minor

| # | Area | File:Line | Issue | Impact | Recommendation |
|---|---|---|---|---|---|
| m-01 | 메모리 | `Services/DigestService.cs:22-69` | `GenerateDigest()` 에서 이미 `RedactedSummary`에 마스킹된 값이 있는데 `_redactionService.Redact(item.RedactedSummary)` 로 **이중 Redact** 수행 | 불필요한 Regex 실행 (4패턴 × 3필드 × 10건 = 120회) | 이미 Redact된 `RedactedSummary`에는 재적용 생략 |
| m-02 | GC | `Services/ClipboardSecurityHelper.cs:35-62` | `StartClearTimer()` 에서 매 `SecureCopy` 호출마다 **새 `DispatcherTimer` 인스턴스 생성**. 이전 타이머는 `Stop()` 후 GC 대상 | 미미하나 비효율적 패턴, GC 압박 | 타이머를 1회 생성 후 재사용 (Stop → Start 패턴) |
| m-03 | 빌드 | `MailTriageAssistant.csproj` | `PublishTrimmed`, `PublishSingleFile` 등 **배포 최적화 옵션 미설정** | 배포 크기 ~150MB+ (self-contained), 시작 시간 추가 ~1s | `<PublishTrimmed>true</PublishTrimmed>`, `<PublishSingleFile>true</PublishSingleFile>`, `<TrimMode>partial</TrimMode>` 추가. AOT는 WPF 미지원 |
| m-04 | COM | `Services/OutlookService.cs:21-46` | 생성자에서 `tcs.Task.GetAwaiter().GetResult()` 로 STA 스레드 생성을 **동기 블로킹** | 앱 시작 ~50-100ms 추가 지연 | 팩토리 패턴 `static async Task<OutlookService> CreateAsync()` 또는 Lazy 초기화 |

### ⚪ Info

| # | Area | File:Line | Issue | Recommendation |
|---|---|---|---|---|
| I-01 | 사양 준수 | `Services/OutlookService.cs:231-234` | `max_body_char_read: 1500` — `body[..1500]` 으로 준수 확인됨 ✅ | — |
| I-02 | 사양 기준 | 전체 | `max_processing_time_per_100_items: 3000ms` — 50개 기준 ~2-4s. 100개 확장 시 목표치 초과 가능성 높음 | C-01, C-02 해결 후 재측정 필요 |

---

## Reliability Matrix

| Scenario | Current Handling | Status | Recommendation |
|---|---|---|---|
| Outlook 미실행 | `EnsureClassicOutlookOrThrow()` → `GetActiveObject` 실패 → `COMException` catch → `InvalidOperationException("Outlook이 실행 중이지 않습니다...")` | ✅ 양호 | — |
| Outlook 중간 종료 | `COMException` catch → `ResetConnection()` + 사용자 안내 메시지 | ⚠️ 부분적 | 자동 재연결 시도 1회 → 실패 시 안내. 현재는 수동 "Run Triage Now" 재클릭 필요 |
| New Outlook 감지 | `Process.GetProcessesByName("olk")` → `NotSupportedException` | ⚠️ False Positive 가능 | 앱 시작 시 1회 검사 + 캐싱. olk.exe 유무만으로 판단하지 않고 COM 초기화 결과로 최종 결정 |
| DMA 0건 Inbox | `FetchInboxHeadersInternal()` 정상 동작 → 빈 리스트 → "표시할 메일이 없습니다." | ✅ 양호 | — |
| COM Timeout/Hang | `InvokeAsync` 에 타임아웃 없음 → **앱 hang** | ❌ 미처리 | M-05: 15초 타임아웃 적용 |
| 개별 이메일 읽기 실패 | `FetchInboxHeadersInternal()` — 개별 아이템 `COMException` 시 **전체 실패** | ❌ 미처리 | 아이템별 try-catch + partial failure 허용 |

---

## Codex Handoff

### 성능 개선 작업 요약

| # | 대상 함수 | 현재 방식 | 개선 방식 | 예상 효과 |
|---|---|---|---|---|
| 1 | `OutlookService.FetchInboxHeadersInternal()` | `Items.Sort()` + 인덱서 접근 | `Items.Restrict()` + `GetFirst()/GetNext()` | 헤더 로드 ~60-70% 단축 |
| 2 | `OutlookService.InvokeAsync()` | 타임아웃 없음 | `CancellationToken` + 15s 타임아웃 | Hang 방지 |
| 3 | `MainViewModel.LoadEmailsAsync()` | `Emails.Add()` 50회 개별 | Batch 교체 (RangeObservableCollection) | UI 플리커 제거 |
| 4 | `MainViewModel.GenerateDigestAsync()` | 순차 `GetBody()` × 10 | Top-10 백그라운드 프리페치 | Digest ~40% 단축 |
| 5 | `OutlookService.EnsureClassicOutlookOrThrow()` | 매 호출 프로세스 스캔 | 1회 검사 + 캐시 플래그 | 호출당 ~10ms 절약 |
| 6 | `OutlookService.FetchInboxHeadersInternal()` | 개별 실패 → 전체 실패 | per-item try-catch + partial failure | 안정성 향상 |
| 7 | `DigestService.GenerateDigest()` | 이미 Redact된 필드 재Redact | 이중 Redact 방지 | Regex 120회 절감 |
| 8 | `ClipboardSecurityHelper.StartClearTimer()` | 매번 새 타이머 생성 | 타이머 재사용 | GC 압박 감소 |
| 9 | `MainWindow.xaml` ListBox | 가상화 미설정 | VirtualizingStackPanel 속성 추가 | 렌더링 최적화 |
| 10 | `MailTriageAssistant.csproj` | Trimming 미설정 | PublishTrimmed + SingleFile | 배포 크기/시작 개선 |
| 11 | 전체 주요 메서드 | 성능 측정 없음 | Stopwatch 계측 코드 삽입 | 개선 효과 정량 측정 |

---

## Task List (Codex 구현용 — 상세)

---

### Task 1: FetchInboxHeaders — Restrict + GetFirst/GetNext 최적화
- **파일**: `MailTriageAssistant/Services/OutlookService.cs`
- **함수**: `FetchInboxHeadersInternal()` (L127–L210)
- **수정 요지**:
  1. `items.Sort("[ReceivedTime]", true)` 제거
  2. 최근 7일 필터: `items.Restrict("[ReceivedTime] >= '" + cutoff.ToString("g") + "'")` 적용
  3. `for (i=1; ...)` 인덱서 루프 → `filtered.GetFirst()` + `filtered.GetNext()` while 루프로 교체
  4. `SafeReleaseComObject(filtered)` 를 finally 블록에 추가
  5. sort 대신 C# 측에서 `result.OrderByDescending(h => h.ReceivedTime).Take(50).ToList()` 후 반환
- **코드 스케치**:
  ```csharp
  private List<RawEmailHeader> FetchInboxHeadersInternal()
  {
      EnsureClassicOutlookOrThrow();
      Outlook.MAPIFolder? inbox = null;
      Outlook.Items? items = null;
      Outlook.Items? filtered = null;
      object? raw = null;
      try
      {
          inbox = _session!.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);
          items = inbox.Items;
          var cutoff = DateTime.Now.AddDays(-7).ToString("g");
          filtered = items.Restrict($"[ReceivedTime] >= '{cutoff}'");
          filtered.Sort("[ReceivedTime]", true);

          var result = new List<RawEmailHeader>(capacity: 50);
          raw = filtered.GetFirst();
          while (raw is not null && result.Count < 50)
          {
              if (raw is Outlook.MailItem mail)
              {
                  Outlook.Attachments? attachments = null;
                  bool hasAttachments;
                  try
                  {
                      attachments = mail.Attachments;
                      hasAttachments = attachments is not null && attachments.Count > 0;
                  }
                  finally
                  {
                      SafeReleaseComObject(attachments);
                  }

                  result.Add(new RawEmailHeader
                  {
                      EntryId = mail.EntryID ?? string.Empty,
                      SenderName = mail.SenderName ?? string.Empty,
                      SenderEmail = mail.SenderEmailAddress ?? string.Empty,
                      Subject = mail.Subject ?? string.Empty,
                      ReceivedTime = mail.ReceivedTime,
                      HasAttachments = hasAttachments,
                  });
              }
              SafeReleaseComObject(raw);
              raw = filtered.GetNext();
          }
          return result;
      }
      catch (COMException)
      {
          ResetConnection();
          throw new InvalidOperationException(
              "Outlook과 통신할 수 없습니다. Classic Outlook이 실행 중인지 확인해 주세요.");
      }
      catch (NotSupportedException) { throw; }
      catch (InvalidOperationException) { throw; }
      catch
      {
          ResetConnection();
          throw new InvalidOperationException("메일 헤더를 불러오는 중 오류가 발생했습니다.");
      }
      finally
      {
          SafeReleaseComObject(raw);
          SafeReleaseComObject(filtered);
          SafeReleaseComObject(items);
          SafeReleaseComObject(inbox);
      }
  }
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: 앱 실행 → "Run Triage Now" → 50개 이메일 표시 확인 + Debug 출력에서 `[PERF]` 시간 측정
- **커밋**: `[05] perf: FetchInboxHeaders에 Restrict+GetFirst/GetNext 패턴 적용`

---

### Task 2: InvokeAsync — 15초 COM 타임아웃 추가
- **파일**: `MailTriageAssistant/Services/OutlookService.cs`
- **함수**: `InvokeAsync<T>()` (L57–L58), `InvokeAsync()` (L60–L61)
- **수정 요지**:
  1. `private static readonly TimeSpan ComTimeout = TimeSpan.FromSeconds(15);` 상수 추가
  2. `InvokeAsync<T>` 내부에서 `Task.WhenAny(dispatcherTask, Task.Delay(ComTimeout))` 적용
  3. 타임아웃 발생 시 `ResetConnection()` 호출 + `InvalidOperationException("Outlook이 응답하지 않습니다.")` throw
- **코드 스케치**:
  ```csharp
  private static readonly TimeSpan ComTimeout = TimeSpan.FromSeconds(15);

  private async Task<T> InvokeAsync<T>(Func<T> func)
  {
      var task = _comDispatcher.InvokeAsync(func).Task;
      if (await Task.WhenAny(task, Task.Delay(ComTimeout)).ConfigureAwait(false) != task)
      {
          ResetConnection();
          throw new InvalidOperationException(
              "Outlook이 응답하지 않습니다. 잠시 후 다시 시도해 주세요.");
      }
      return await task.ConfigureAwait(false);
  }

  private async Task InvokeAsync(Action action)
  {
      var task = _comDispatcher.InvokeAsync(action).Task;
      if (await Task.WhenAny(task, Task.Delay(ComTimeout)).ConfigureAwait(false) != task)
      {
          ResetConnection();
          throw new InvalidOperationException(
              "Outlook이 응답하지 않습니다. 잠시 후 다시 시도해 주세요.");
      }
      await task.ConfigureAwait(false);
  }
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: Outlook 종료 상태에서 "Run Triage Now" → 15초 내 에러 메시지 노출 확인
- **커밋**: `[05] reliability: InvokeAsync에 15초 COM 타임아웃 추가`

---

### Task 3: FetchInboxHeaders — 개별 아이템 partial failure 허용
- **파일**: `MailTriageAssistant/Services/OutlookService.cs`
- **함수**: `FetchInboxHeadersInternal()` — while 루프 내부 (Task 1에서 변경된 코드 기준)
- **수정 요지**:
  1. 각 아이템 읽기(`mail.EntryID`, `mail.SenderName` 등)를 `try-catch(COMException)` 로 감싸기
  2. 개별 실패 시 `Debug.WriteLine($"[PERF] Item skipped: {ex.ErrorCode}")` 후 `continue`
  3. 항목 수 부족 시 제한 없이 다음 항목 시도 (fetched 카운트는 성공건만 증가)
- **코드 스케치** (while 루프 내부):
  ```csharp
  raw = filtered.GetFirst();
  while (raw is not null && result.Count < 50)
  {
      try
      {
          if (raw is Outlook.MailItem mail)
          {
              // ... 헤더 추출 ...
              result.Add(new RawEmailHeader { ... });
          }
      }
      catch (COMException ex)
      {
          Debug.WriteLine($"[PERF] Item skipped: 0x{ex.ErrorCode:X8}");
      }
      finally
      {
          SafeReleaseComObject(raw);
      }
      raw = filtered.GetNext();
  }
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: 삭제된 메일이 포함된 Inbox에서 Run Triage → 나머지 항목 정상 표시
- **커밋**: `[05] reliability: FetchInboxHeaders 개별 아이템 partial failure 허용`

---

### Task 4: ObservableCollection Batch 갱신 (RangeObservableCollection)
- **파일(수정)**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **파일(신규)**: `MailTriageAssistant/Helpers/RangeObservableCollection.cs`
- **함수**: `LoadEmailsAsync()` (L113–L173), `Emails` 프로퍼티 (L30)
- **수정 요지**:
  1. `Helpers/RangeObservableCollection.cs` 생성:
     ```csharp
     using System.Collections.Generic;
     using System.Collections.ObjectModel;
     using System.Collections.Specialized;

     namespace MailTriageAssistant.Helpers;

     public class RangeObservableCollection<T> : ObservableCollection<T>
     {
         private bool _suppressNotification;

         public void AddRange(IEnumerable<T> items)
         {
             _suppressNotification = true;
             foreach (var item in items)
             {
                 Items.Add(item);
             }
             _suppressNotification = false;
             OnCollectionChanged(
                 new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
         }

         protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
         {
             if (!_suppressNotification)
                 base.OnCollectionChanged(e);
         }
     }
     ```
  2. `MainViewModel.Emails` 타입을 `RangeObservableCollection<AnalyzedItem>` 로 변경
  3. `LoadEmailsAsync()` 의 `foreach (var item in analyzed...) Emails.Add(item)` → `Emails.AddRange(analyzed.OrderByDescending(...).ThenByDescending(...))` 1회 호출
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: Run Triage → 리스트가 한 번에 렌더링 (플리커 없음) 확인
- **커밋**: `[05] perf: RangeObservableCollection으로 Batch 갱신 적용`

---

### Task 5: EnsureClassicOutlookOrThrow — 프로세스 검사 1회 캐싱
- **파일**: `MailTriageAssistant/Services/OutlookService.cs`
- **함수**: `EnsureClassicOutlookOrThrow()` (L63–L95)
- **수정 요지**:
  1. `private bool _newOutlookChecked;` 필드 추가
  2. `Process.GetProcessesByName("olk")` 호출을 `if (!_newOutlookChecked)` 로 감싸기
  3. COM 연결 성공 시 `_newOutlookChecked = true;`
  4. `ResetConnection()` 에서 `_newOutlookChecked = false;` (재연결 시 재검사)
- **코드 스케치**:
  ```csharp
  private bool _newOutlookChecked;

  private void EnsureClassicOutlookOrThrow()
  {
      if (!_newOutlookChecked)
      {
          if (Process.GetProcessesByName("olk").Any())
          {
              throw new NotSupportedException(
                  "Classic Outlook이 필요합니다. New Outlook(olk.exe)은 COM Interop을 지원하지 않습니다.");
          }
      }

      if (_app is not null && _session is not null)
      {
          return;
      }

      try
      {
          _app = GetActiveOutlookApplication();
          _session = _app.Session;
          _newOutlookChecked = true;  // 성공 후 캐싱
      }
      catch (COMException) { ... }
      catch (Exception) { ... }
  }
  ```
  `ResetConnection()` 에 추가:
  ```csharp
  private void ResetConnection()
  {
      _newOutlookChecked = false;  // 추가
      SafeReleaseComObject(_session);
      SafeReleaseComObject(_app);
      _session = null;
      _app = null;
  }
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: 디버거에서 2번째 COM 호출부터 `Process.GetProcessesByName` 스킵 확인
- **커밋**: `[05] perf: New Outlook 프로세스 검사 1회 캐싱`

---

### Task 6: Top-10 본문 백그라운드 프리페치
- **파일**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **함수**: `LoadEmailsAsync()` (L113–L173) — 끝 부분, 신규 `PrefetchTopBodiesAsync()`
- **수정 요지**:
  1. `LoadEmailsAsync()` 의 `finally` 직전에 `_ = PrefetchTopBodiesAsync();` fire-and-forget 추가
  2. 신규 메서드 `PrefetchTopBodiesAsync()`:
     - Top-10 우선순위 이메일 중 `IsBodyLoaded == false` 항목만 추출
     - 순차적으로 `GetBody()` → `AnalyzeWithBody()` → `RedactedSummary` 설정
     - 개별 실패는 무시 (프리페치 실패 시 Digest 생성 시점에 재시도)
  3. `GenerateDigestAsync()` 에서 이미 `IsBodyLoaded` 인 항목은 스킵 (기존 로직 유지)
- **코드 스케치**:
  ```csharp
  private async Task PrefetchTopBodiesAsync()
  {
      var top = Emails
          .OrderByDescending(e => e.Score)
          .ThenByDescending(e => e.ReceivedTime)
          .Take(10)
          .Where(e => !e.IsBodyLoaded)
          .ToList();

      foreach (var item in top)
      {
          try
          {
              var body = await _outlookService.GetBody(item.EntryId).ConfigureAwait(true);
              var triage = _triageService.AnalyzeWithBody(item.SenderEmail, item.Subject, body);
              item.Category = triage.Category;
              item.Score = triage.Score;
              item.ActionHint = triage.ActionHint;
              item.Tags = triage.Tags;
              item.RedactedSummary = string.IsNullOrWhiteSpace(body)
                  ? "(본문이 비어 있습니다.)"
                  : _redactionService.Redact(body);
              item.IsBodyLoaded = true;
          }
          catch
          {
              // 프리페치 실패는 무시 — Digest 시점에 재시도됨
          }
      }
  }
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: Run Triage 직후 즉시 "Copy Digest & Open Teams" → 대기 시간 감소 확인
- **커밋**: `[05] perf: Top-10 본문 백그라운드 프리페치로 Digest 생성 시간 단축`

---

### Task 7: DigestService 이중 Redact 방지
- **파일**: `MailTriageAssistant/Services/DigestService.cs`
- **함수**: `GenerateDigest()` (L22–L69)
- **수정 요지**:
  1. L45: `var summary = EscapeCell(_redactionService.Redact(item.RedactedSummary))` → `var summary = EscapeCell(item.RedactedSummary ?? string.Empty)` 로 변경
  2. Sender/Subject는 원본이므로 `Redact()` 유지
  3. 주석 추가: `// RedactedSummary는 ViewModel에서 이미 마스킹 완료`
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: Digest 텍스트에서 이중 마스킹 (예: `[[EMAIL]]`) 패턴 없는지 확인
- **커밋**: `[05] perf: DigestService에서 이미 Redact된 summary 재마스킹 제거`

---

### Task 8: ClipboardSecurityHelper 타이머 인스턴스 재사용
- **파일**: `MailTriageAssistant/Services/ClipboardSecurityHelper.cs`
- **함수**: 생성자, `StartClearTimer()` (L31–L63)
- **수정 요지**:
  1. 생성자에서 `_clearTimer` 1회 생성 + Tick 핸들러 등록
  2. `StartClearTimer()` → `_clearTimer.Stop()` + `_clearTimer.Start()` 만 수행
  3. Tick 핸들러를 별도 메서드 `OnClearTimerTick(object?, EventArgs)` 로 분리
- **코드 스케치**:
  ```csharp
  public ClipboardSecurityHelper(RedactionService redactionService)
  {
      _redactionService = redactionService
          ?? throw new ArgumentNullException(nameof(redactionService));

      _clearTimer = new DispatcherTimer(DispatcherPriority.Background)
      {
          Interval = TimeSpan.FromSeconds(30),
      };
      _clearTimer.Tick += OnClearTimerTick;
  }

  private void OnClearTimerTick(object? sender, EventArgs e)
  {
      _clearTimer!.Stop();
      try
      {
          if (_copiedContent is not null &&
              Clipboard.ContainsText() &&
              string.Equals(Clipboard.GetText(), _copiedContent, StringComparison.Ordinal))
          {
              Clipboard.Clear();
          }
      }
      catch { }
      finally
      {
          _copiedContent = null;
      }
  }

  private void StartClearTimer()
  {
      _clearTimer!.Stop();
      _clearTimer.Start();
  }
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: "Copy For Copilot" 3회 연속 → 30초 후 클립보드 삭제 정상 동작
- **커밋**: `[05] perf: ClipboardSecurityHelper DispatcherTimer 인스턴스 재사용`

---

### Task 9: MainWindow.xaml ListBox 가상화 설정
- **파일**: `MailTriageAssistant/MainWindow.xaml`
- **위치**: `<ListBox>` 태그 (L66–L104)
- **수정 요지**:
  1. `<ListBox>` 에 다음 속성 추가:
     ```xml
     VirtualizingStackPanel.IsVirtualizing="True"
     VirtualizingStackPanel.VirtualizationMode="Recycling"
     ScrollViewer.CanContentScroll="True"
     ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: Run Triage → 리스트 스크롤 시 부드러운 렌더링 확인
- **커밋**: `[05] perf: ListBox에 VirtualizingStackPanel Recycling 모드 추가`

---

### Task 10: csproj 빌드/배포 최적화 (Trimming)
- **파일**: `MailTriageAssistant/MailTriageAssistant.csproj`
- **수정 요지**:
  1. `<PropertyGroup>` 에 추가:
     ```xml
     <PublishTrimmed>true</PublishTrimmed>
     <PublishSingleFile>true</PublishSingleFile>
     <SelfContained>true</SelfContained>
     <TrimMode>partial</TrimMode>
     ```
  2. COM Interop Trim 제외 (리플렉션 의존):
     ```xml
     <ItemGroup>
       <TrimmerRootAssembly Include="Microsoft.Office.Interop.Outlook" />
     </ItemGroup>
     ```
  3. ⚠️ `TrimMode="partial"` 사용 — WPF 바인딩 리플렉션 보호. `full` 사용 시 런타임 에러 가능
- **테스트 명령**: `dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x64`
- **수동 검증**: publish 폴더 크기 확인 (<80MB 목표), 실행 후 전체 기능 정상 동작
- **커밋**: `[05] build: PublishTrimmed + SingleFile 설정 추가`

---

### Task 11: Stopwatch 성능 계측 코드 삽입
- **파일(수정)**: `MailTriageAssistant/Services/OutlookService.cs`, `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **대상 함수**:
  - `OutlookService.FetchInboxHeadersInternal()` — 전체 실행 시간
  - `OutlookService.GetBodyInternal()` — 단건 로드 시간
  - `MainViewModel.LoadEmailsAsync()` — 헤더 fetch + triage 분석 시간
  - `MainViewModel.GenerateDigestAsync()` — Digest 생성 전체 시간
- **수정 요지**:
  1. 각 메서드 시작/끝에 `Stopwatch` 기반 계측 삽입
  2. `#if DEBUG` 컴파일러 지시문으로 감싸서 Release 빌드에서 제거
  3. **본문 내용은 절대 출력 금지** — 시간·건수만 기록
- **코드 스케치**:
  ```csharp
  #if DEBUG
  var sw = System.Diagnostics.Stopwatch.StartNew();
  #endif

  // ... 대상 코드 ...

  #if DEBUG
  sw.Stop();
  System.Diagnostics.Debug.WriteLine(
      $"[PERF] {nameof(FetchInboxHeadersInternal)}: {sw.ElapsedMilliseconds}ms (items: {result.Count})");
  // ⚠️ 본문 내용은 절대 출력 금지
  #endif
  ```
- **테스트 명령**: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj`
- **수동 검증**: Debug 출력 창에서 `[PERF]` 로그 확인, 각 메서드별 소요 시간 기록
- **커밋**: `[05] perf: 주요 메서드에 Stopwatch 성능 계측 코드 삽입`

---

## 커밋 절차 (모든 Task 공통)

```
1) 성능/안정성 개선 1건 수정
2) dotnet build MailTriageAssistant/MailTriageAssistant.csproj → 빌드 성공 확인
3) 수동 테스트: 이메일 50개 로드 / Digest 생성 / Reply 동작 확인
4) 커밋: [05] perf: {한줄 설명}  또는  [05] reliability: {한줄 설명}
```

## 우선순위 실행 순서

```
Phase 1 (Critical — 즉시):  Task 1 → Task 2 → Task 3
Phase 2 (Major — 다음):     Task 4 → Task 5 → Task 6
Phase 3 (Minor — 완료 후):  Task 7 → Task 8 → Task 9 → Task 10
Phase 4 (계측):             Task 11
```

## 측정 방법 (Codex가 삽입할 코드)

```csharp
#if DEBUG
var sw = Stopwatch.StartNew();
#endif
// ... 대상 코드 ...
#if DEBUG
sw.Stop();
Debug.WriteLine($"[PERF] {methodName}: {sw.ElapsedMilliseconds}ms");
// ⚠️ 본문 내용은 절대 출력 금지
#endif
```
