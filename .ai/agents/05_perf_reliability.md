# Agent 05: Performance & Reliability

## Mission
앱의 시작 시간, 이메일 로딩 속도, 메모리 사용량, COM 안정성, 에러 복구력을 점검하고 개선 방안을 도출한다.

## Scope
- COM Interop 성능 (STA 스레딩, 객체 해제)
- 이메일 데이터 로딩 전략 (3단계 로딩)
- UI 스레드 응답성 (async/await, Dispatcher)
- 메모리 관리 (대량 이메일 처리 시)
- 에러 복구 (Outlook 재시작, COM 연결 끊김)

## Non-Goals
- 네트워크 성능 (로컬 앱이므로)
- 데이터베이스 성능 (DB 없음)

---

## Inputs (우선순위 파일 목록)

| 우선순위 | 파일 | 확인 포인트 |
|---|---|---|
| P0 | `Services/OutlookService.cs` | STA 스레드 관리, COM 해제, 재연결, FetchInboxHeaders 성능 |
| P0 | `ViewModels/MainViewModel.cs` | async/await 패턴, UI freeze, ObservableCollection 갱신 |
| P1 | `Services/TriageService.cs` | 키워드 매칭 효율성 (50개 이메일 × 다수 키워드) |
| P1 | `Services/DigestService.cs` | StringBuilder 사용, 대량 항목 처리 |
| P1 | `Services/RedactionService.cs` | Regex 컴파일 최적화 (RegexOptions.Compiled) |
| P2 | `Services/ClipboardSecurityHelper.cs` | DispatcherTimer 정확성, GC 영향 |
| P2 | `MainWindow.xaml` | 가상화 (VirtualizingStackPanel), 리스트 렌더링 |
| P3 | `MailTriageAssistant.csproj` | 빌드 최적화, Trimming, AOT |

---

## Review Checklist

### COM Interop 성능
- [ ] STA 스레드 전용 Outlook 액세스 (현재: 별도 Thread + Dispatcher)
- [ ] COM 객체 `Marshal.ReleaseComObject()` 호출 완전성
- [ ] `FetchInboxHeaders()` 에서 Body 미접근 확인 (성능 핵심)
- [ ] `Items.Sort()` vs `Items.Restrict()` 선택 적절성
- [ ] 대량 아이템 시 `for` vs `foreach` 성능

### UI 응답성
- [ ] `LoadEmailsAsync()` 가 UI 스레드를 블로킹하지 않음
- [ ] `ObservableCollection` 갱신 시 `Dispatcher.Invoke` 사용
- [ ] 프로그레스 인디케이터 동작 (IsLoading)
- [ ] 대량 항목 리스트 가상화(`VirtualizingStackPanel.VirtualizationMode`)

### 메모리 관리
- [ ] 이메일 본문 캐시 크기 제한 (50 × 1500자 = 75KB)
- [ ] `RawEmailHeader` → `AnalyzedItem` 변환 시 불필요한 복사 없음
- [ ] GC 압박 (string 잦은 생성)
- [ ] COM 래퍼 누수 여부

### 에러 복구
- [ ] Outlook 종료/재시작 시 앱 대응 (ResetConnection)
- [ ] COM 연결 끊김 → 자동 재연결 or 사용자 안내
- [ ] 타임아웃 처리 (Outlook 무응답 시)
- [ ] 개별 이메일 읽기 실패 시 전체 실패 방지 (partial failure)

### 사양서 성능 기준
- [ ] `max_body_char_read: 1500` 준수 확인
- [ ] `max_processing_time_per_100_items: 3000ms` 달성 가능 여부

---

## Output Template

산출물 경로: `.ai/reports/YYYY-MM-DD_perf_reliability.md`

```markdown
# Performance & Reliability Report — MailTriageAssistant
> Date: YYYY-MM-DD
> Reviewer: Agent 05 (Performance & Reliability)

## Summary
- Total Issues: N
- Critical: N | Major: N | Minor: N | Info: N

## Performance Baseline (추정)
| Metric | Current (Est.) | Target | Gap |
|---|---|---|---|
| 앱 시작 시간 | ~Xs | <3s | ? |
| 50 이메일 헤더 로드 | ~Xms | <1000ms | ? |
| 본문 1건 로드 | ~Xms | <200ms | ? |
| Digest 생성 (10건) | ~Xms | <500ms | ? |

## Findings

### 🔴 Critical
| # | Area | Issue | Impact | Recommendation |
|---|---|---|---|---|

### 🟡 Major
| # | Area | Issue | Impact | Recommendation |
|---|---|---|---|---|

### 🟢 Minor / ⚪ Info
(생략 가능)

## Reliability Matrix
| Scenario | Current Handling | Recommendation |
|---|---|---|
| Outlook 미실행 | ? | ? |
| Outlook 중간 종료 | ? | ? |
| New Outlook 감지 | ? | ? |
| DMA 0건 Inbox | ? | ? |

## Codex Handoff
```

---

## Codex Handoff

1. **성능 개선 작업 목록**
   - 각 항목: `대상 함수`, `현재 방식`, `개선 방식`, `예상 효과`

2. **커밋 절차**
   ```
   1) 성능 개선 1건 수정
   2) dotnet build → 성공
   3) 수동 테스트: 이메일 50개 로드 시간 비교
   4) 커밋: [05] perf: {한줄 설명}
   ```

3. **측정 방법 (Codex가 삽입할 코드)**
   ```csharp
   var sw = Stopwatch.StartNew();
   // ... 대상 코드 ...
   sw.Stop();
   Debug.WriteLine($"[PERF] {methodName}: {sw.ElapsedMilliseconds}ms");
   // ⚠️ 본문 내용은 절대 출력 금지
   ```
