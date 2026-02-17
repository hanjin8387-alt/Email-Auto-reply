# Agent 06: Performance & Reliability
> Role: 성능 병목·메모리·네트워크·비동기·장애복구·리트라이·타임아웃 분석

---

## Mission
`MailTriageAssistant`의 런타임 성능 특성과 안정성을 분석한다. COM Interop 병목, UI 응답성, 메모리 관리, 에러 복구 전략을 평가하고 개선안을 제시한다.

## Scope
- COM Interop 성능 (STA 스레드, 인덱서 접근 패턴, 타임아웃)
- UI 스레드 응답성 (비동기 패턴, ObservableCollection 갱신)
- 메모리 (COM RCW 누적, DispatcherTimer 누수, GC 압박)
- 비동기 패턴 (fire-and-forget, ConfigureAwait, 예외 전파)
- 장애 복구 (Outlook 중간 종료, COM Timeout, Partial Failure)
- 리트라이/타임아웃 전략
- 빌드/배포 최적화 (Trimming, SingleFile)

## Non-Goals
- 보안 취약점 분석 (07 에이전트)
- UI 디자인 (02 에이전트)

---

## Inputs (우선순위)

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `Services/OutlookService.cs` | COM 성능 핫스팟 (541줄) |
| P0 | `ViewModels/MainViewModel.cs` | 비동기 패턴, UI 갱신 (538줄) |
| P1 | `Services/ClipboardSecurityHelper.cs` | 타이머 관리 |
| P1 | `Services/TriageService.cs` | 키워드 검색 효율 |
| P1 | `Helpers/RangeObservableCollection.cs` | Batch 갱신 패턴 |
| P2 | `MailTriageAssistant.csproj` | 빌드 최적화 설정 |
| P2 | `MainWindow.xaml` | UI 가상화 |

---

## Checklist

### COM Interop
- [ ] `Items.Restrict()` vs `Items.Sort()` 패턴
- [ ] `GetFirst()/GetNext()` vs 인덱서 접근
- [ ] `SafeReleaseComObject()` 일관 사용
- [ ] `InvokeAsync` 타임아웃 적용
- [ ] Partial Failure 허용

### UI 응답성
- [ ] `ObservableCollection` Batch 갱신
- [ ] `VirtualizingStackPanel` 설정
- [ ] 로딩 인디케이터 표시
- [ ] fire-and-forget 안전 래퍼

### 메모리
- [ ] IDisposable 구현 + DI 생명주기
- [ ] DispatcherTimer 단일 인스턴스
- [ ] COM RCW 해제 패턴

### 장애 복구
- [ ] Outlook 미실행 시 에러 메시지
- [ ] Outlook 중간 종료 시 ResetConnection
- [ ] COM Timeout (15~30초)
- [ ] 개별 아이템 실패 시 건너뛰기

---

## Output Template

```markdown
# Performance & Reliability Report — MailTriageAssistant
> Date: YYYY-MM-DD

## Summary
- 총 이슈: N | Critical: N | Major: N | Minor: N | Info: N

## Performance Baseline (추정)
| 지표 | 현재 (추정) | 목표 | 갭 |
|---|---|---|---|

## Findings
### 🔴 Critical
| # | 영역 | 파일:함수 | 이슈 | 영향 | 권장사항 |

### 🟡 Major
| # | 영역 | 파일:함수 | 이슈 | 영향 | 권장사항 |

### 🟢 Minor
| # | 영역 | 파일:함수 | 이슈 | 영향 | 권장사항 |

## Reliability Matrix
| 시나리오 | 현재 처리 | 상태 | 권장사항 |
|---|---|---|---|

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 예상 효과 | 테스트 커맨드 | 위험도 |
```

---

## Codex Handoff Contract

| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | 성능/안정성 수정 대상 |
| 변경 요지 | ✅ | 무엇을 왜 최적화하는지 |
| 예상 효과 | ✅ | ~N% 단축, GC 압박 감소 등 |
| 측정 방법 | ❌ | Stopwatch, ETW, Debug 출력 |
| 테스트 커맨드 | ✅ | `dotnet build` 또는 수동 검증 |
| 커밋 메시지 | ✅ | `[06] perf: {설명}` 또는 `[06] reliability: {설명}` |

---

## Stop Conditions

| 조건 | 대응 |
|---|---|
| 성능 최적화가 가독성을 심각히 저해 | 최적화 보류 + 코멘트로 의도 기록 |
| COM 패턴 변경이 Classic Outlook 호환성 깨짐 | 즉시 중단 + 대안 경로 문서화 |
| 메모리 누수 수정이 기능 변경과 충돌 | 누수 수정 선행 → 기능 변경 후행 |
