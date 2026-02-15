# Agent 06: Security & Privacy

## Mission
이메일 본문 유출 경로, PII 마스킹 완전성, 클립보드 보안, COM 보안, 인젝션 위험을 점검하고 보안 강화 방안을 도출한다.

## Scope
- PII 마스킹 패턴 완전성 (RedactionService)
- 클립보드 데이터 수명 (ClipboardSecurityHelper)
- 본문 로그 유출 경로 (전체 코드 스캔)
- 예외 메시지 내 데이터 노출
- COM 보안 (권한 에스컬레이션, RPC)
- 입·출력 인젝션 (Markdown 인젝션, 템플릿 인젝션)

## Non-Goals
- 네트워크 보안 (로컬 앱, 네트워크 통신 없음)
- 인증/인가 (싱글 유저 데스크톱 앱)

---

## Inputs (우선순위 파일 목록)

| 우선순위 | 파일 | 확인 포인트 |
|---|---|---|
| P0 | `Services/RedactionService.cs` | 패턴 누락(IP, 계좌번호, 여권), 우회 가능 입력, 유니코드 |
| P0 | `Services/ClipboardSecurityHelper.cs` | 타이머 신뢰성, 레이스 컨디션, Win+V 히스토리 |
| P0 | `Services/OutlookService.cs` | 본문 로그, 예외 메시지, COM 권한 |
| P0 | `ViewModels/MainViewModel.cs` | 본문 캐시 수명, 에러 핸들링 시 데이터 노출 |
| P1 | `Services/DigestService.cs` | Markdown 인젝션, 클립보드 데이터 |
| P1 | `Services/TriageService.cs` | 키워드 기반 분류가 본문 내용을 외부에 노출하는지 |
| P1 | `Services/TemplateService.cs` | 템플릿 인젝션 (사용자 입력 → 이메일 본문) |
| P2 | `MainWindow.xaml` | 바인딩을 통한 비마스킹 데이터 노출 |
| P2 | `MailTriageAssistant.csproj` | 의존성 취약점 |

---

## Review Checklist

### PII 마스킹 검증
- [ ] 한국 전화번호: `010-XXXX-XXXX` ✅ 구현
- [ ] 한국 주민번호: `XXXXXX-XXXXXXX` ✅ 구현
- [ ] 이메일: `user@domain.com` ✅ 구현
- [ ] 신용카드: `XXXX-XXXX-XXXX-XXXX` ✅ 구현
- [ ] **미구현 패턴:**
  - [ ] 한국 계좌번호 (은행별 형식)
  - [ ] 여권번호 (`M12345678`)
  - [ ] IP 주소 (`192.168.x.x`)
  - [ ] URL에 포함된 토큰/키
- [ ] 유니코드 변형 우회 (전각 숫자 등)
- [ ] 패턴 순서 충돌 (신용카드 vs 일반 숫자)

### 데이터 유출 경로
- [ ] `Console.WriteLine` 호출 전수 조사
- [ ] `Debug.WriteLine` 호출 전수 조사
- [ ] `Trace.Write*` 호출 전수 조사
- [ ] `MessageBox.Show` 에 본문 포함 여부
- [ ] 예외 `Message` / `StackTrace` 에 본문 포함 가능성
- [ ] WPF 바인딩 오류 시 Output 창에 데이터 노출
- [ ] `ToString()` 오버라이드로 인한 데이터 노출

### 클립보드 보안
- [ ] 30초 자동 삭제 동작 확인
- [ ] `Clipboard.ContainsText()` 레이스 컨디션
- [ ] Windows 클립보드 히스토리(Win+V) 대응
- [ ] 다른 프로세스의 클립보드 접근

### COM 보안
- [ ] DCOM 권한 설정 (로컬 실행 전용)
- [ ] `Marshal.ReleaseComObject` 후 재접근 방지
- [ ] RPC 타임아웃 설정

### 의존성 보안
- [ ] NuGet 패키지 알려진 취약점 (`dotnet list package --vulnerable`)
- [ ] `Microsoft.Office.Interop.Outlook` 버전 최신 여부

---

## Output Template

산출물 경로: `.ai/reports/YYYY-MM-DD_security_privacy.md`

```markdown
# Security & Privacy Report — MailTriageAssistant
> Date: YYYY-MM-DD
> Reviewer: Agent 06 (Security & Privacy)
> Classification: CONFIDENTIAL

## Summary
- Total Issues: N
- Critical: N | Major: N | Minor: N | Info: N

## Threat Model Summary
| Threat | Vector | Current Mitigation | Gap |
|---|---|---|---|
| PII 유출 (클립보드) | 다른 앱 읽기 | 30초 자동삭제 | Win+V 히스토리 |
| PII 유출 (로그) | Debug 출력 | 코드 규칙 | 자동 검증 없음 |
| ... | ... | ... | ... |

## Findings

### 🔴 Critical
| # | Category | File | Line | Issue | CVSS (Est.) | Recommendation |
|---|---|---|---|---|---|---|
| S-1 | PII Leak | file.cs | L42 | 설명 | 7.5 | 수정안 |

### 🟡 Major
| # | Category | File | Line | Issue | CVSS (Est.) | Recommendation |
|---|---|---|---|---|---|---|

### 🟢 Minor / ⚪ Info
(생략 가능)

## Redaction Coverage Matrix
| PII Type | Pattern | Status | Notes |
|---|---|---|---|
| 전화번호 | 010-\d{4}-\d{4} | ✅ | |
| 주민번호 | \d{6}-\d{7} | ✅ | |
| 이메일 | regex | ✅ | |
| 신용카드 | \d{4}-\d{4}-\d{4}-\d{4} | ✅ | |
| 계좌번호 | — | ❌ | 미구현 |
| IP 주소 | — | ❌ | 미구현 |

## Codex Handoff
```

---

## Codex Handoff

1. **보안 수정 작업 목록**
   - Critical 항목은 즉시 수정
   - 각 수정에 대해 **역테스트** 추가 (마스킹 우회 시도)

2. **커밋 절차**
   ```
   1) 보안 수정 1건
   2) 역테스트 코드 작성 (우회 시도가 실패하는지)
   3) dotnet build + dotnet test
   4) 커밋: [06] security: {한줄 설명}
   ```

3. **코드 스캔 명령 (Codex가 실행)**
   ```bash
   # 본문 로그 유출 검색
   grep -rn "Console.Write\|Debug.Write\|Trace.Write" Services/ ViewModels/
   
   # 의존성 취약점 검사
   dotnet list package --vulnerable
   ```

4. **PR 요약 형식**
   ```
   ## Security Fixes (YYYY-MM-DD)
   - Fixed N critical security issues
   - Added N redaction patterns
   - Added N security-related tests
   ⚠️ REQUIRES SECURITY REVIEW BEFORE MERGE
   ```
