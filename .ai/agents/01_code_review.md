# Agent 01: Code Review

## Mission
MailTriageAssistant 코드베이스의 품질, 아키텍처 준수, 코딩 표준 적합성을 점검하고 개선 사항을 도출한다.

## Scope
- 전체 C# 소스 파일 (17개)
- XAML 파일 (2개)
- 프로젝트 설정 (.csproj)

## Non-Goals
- 대규모 코드 수정 (리포트와 작업 목록만 산출)
- UI 디자인 판단 (→ `02_uiux` 담당)
- 성능 벤치마크 (→ `05_perf_reliability` 담당)

---

## Inputs (우선순위 파일 목록)

| 우선순위 | 파일 | 확인 포인트 |
|---|---|---|
| P0 | `Services/OutlookService.cs` (328줄) | COM 리소스 해제, STA 스레딩, COMException 처리, 본문 로그 금지 |
| P0 | `ViewModels/MainViewModel.cs` (379줄) | MVVM 준수, 3단계 로딩 구현 여부, async/await 패턴, UI 스레드 안전성 |
| P0 | `Services/RedactionService.cs` (33줄) | 정규식 완전성, 패턴 우선순위, 엣지 케이스 |
| P1 | `Services/DigestService.cs` (134줄) | Markdown 생성 정합성, 클립보드 보안 연동 |
| P1 | `Services/TriageService.cs` (164줄) | 점수 산출 로직, 키워드 관리, 확장성 |
| P1 | `Services/ClipboardSecurityHelper.cs` | 타이머 해제, 스레드 안전성 |
| P2 | `MainWindow.xaml` (185줄) | 바인딩 정합성, 접근성, 반응형 레이아웃 |
| P2 | `Models/*.cs` | 프로퍼티 알림, null safety |
| P3 | `Helpers/ScoreToColorConverter.cs` | IValueConverter 정합성 |
| P3 | `ViewModels/RelayCommand.cs` | ICommand 구현 |
| P3 | `App.xaml.cs`, `AssemblyInfo.cs` | 앱 초기화, 메타데이터 |

---

## Review Checklist

### 아키텍처 & 설계
- [ ] MVVM 패턴 준수 (코드비하인드에 비즈니스 로직 없음)
- [ ] 서비스 간 순환 의존성 없음
- [ ] `IOutlookService` 인터페이스를 통한 DI 가능 여부
- [ ] 서비스 생명주기 관리 (Singleton vs Transient)

### 코드 품질
- [ ] `using` 문 정리 (미사용 네임스페이스)
- [ ] nullable 참조 타입 일관성 (`#nullable enable`)
- [ ] 매직 넘버/문자열 상수화
- [ ] 메서드 길이 (30줄 이하 권장)
- [ ] 예외 처리 패턴 일관성

### 보안 준수
- [ ] 이메일 본문을 로그에 기록하는 코드 없음
- [ ] `Console.WriteLine` / `Debug.WriteLine` 사용 여부
- [ ] 예외 메시지에 본문 포함 가능성
- [ ] COM 객체 해제 누락

### .NET 모범 사례
- [ ] `async/await` 올바른 사용 (ConfigureAwait, deadlock 방지)
- [ ] `IDisposable` 구현 필요 여부 (COM 래퍼)
- [ ] `string.Empty` vs `""` 일관성
- [ ] LINQ 사용 효율성

---

## Output Template

산출물 경로: `.ai/reports/YYYY-MM-DD_code_review.md`

```markdown
# Code Review Report — MailTriageAssistant
> Date: YYYY-MM-DD
> Reviewer: Agent 01 (Code Review)

## Summary
- Total Issues: N
- Critical: N | Major: N | Minor: N | Info: N

## Findings

### 🔴 Critical
| # | File | Line | Issue | Recommendation |
|---|---|---|---|---|
| C-1 | `file.cs` | L42 | 설명 | 수정안 |

### 🟡 Major
| # | File | Line | Issue | Recommendation |
|---|---|---|---|---|
| M-1 | `file.cs` | L42 | 설명 | 수정안 |

### 🟢 Minor
| # | File | Line | Issue | Recommendation |
|---|---|---|---|---|

### ⚪ Info
| # | File | Line | Issue | Recommendation |
|---|---|---|---|---|

## Architecture Notes
(아키텍처 수준 관찰 사항)

## Codex Handoff
(다음 섹션 참조)
```

---

## Codex Handoff

Codex가 수행할 구체적 작업 절차:

1. **리포트 기반 작업 목록 추출**
   - Critical/Major 항목을 개별 작업으로 분리
   - 각 작업: `파일명`, `함수명`, `변경 요약`, `예상 영향 범위`

2. **커밋 절차**
   ```
   1) 수정 대상 파일 백업 (git stash or branch)
   2) 단일 항목 수정
   3) dotnet build → 성공 확인
   4) dotnet test → 통과 확인 (테스트 존재 시)
   5) 커밋: [01] fix: {한줄 설명}
   6) 다음 항목으로 이동
   ```

3. **변경 로그 형식**
   ```
   | 커밋 | 파일 | 변경 | 위험도 |
   |---|---|---|---|
   | abc1234 | OutlookService.cs | COM 해제 누락 수정 | Critical |
   ```

4. **PR 요약 형식**
   ```
   ## Code Review Fixes (YYYY-MM-DD)
   - Fixed N critical issues
   - Fixed N major issues
   - Deferred N minor items to backlog
   ```
