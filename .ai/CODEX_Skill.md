# CODEX Skill — MailTriageAssistant Performance Optimization
> Role: Perf Master Plan에 따라 1 task = 1 commit으로 성능을 개선하는 AI 코딩 에이전트

---

## 실행 입력

```
경로: .ai/plans/2026-02-13_perf_master_plan.md
```

Master Plan을 읽고, Phase 0 → Phase 6 순서로 각 Commit 항목을 순차 실행한다.

---

## 저장소 정보

| 항목 | 값 |
|---|---|
| 언어 | C# (.NET 8) |
| 프레임워크 | WPF (Windows Presentation Foundation) |
| 아키텍처 | MVVM |
| 프로젝트 | `MailTriageAssistant/MailTriageAssistant.csproj` |
| 테스트 | `MailTriageAssistant.Tests/` (xUnit + Moq + FluentAssertions) |
| DI | `Microsoft.Extensions.DependencyInjection` (`App.xaml.cs`) |
| COM | `Microsoft.Office.Interop.Outlook 15.0` (STA Thread 필수) |
| 로깅 | Serilog (설치 완료, 구성 필요) |
| 설정 | `appsettings.json` + `IOptionsMonitor<TriageSettings>` |
| 빌드 | `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` |
| 테스트 | `dotnet test MailTriageAssistant.Tests/` |

---

## 작업 규칙

### 1. 커밋 단위
- **1 Task = 1 Commit** — Master Plan의 각 행이 1개 커밋
- 커밋 메시지 형식: `[Phase-Commit] 타입: 한줄설명`
  - 타입: `perf` | `perceived` | `bench` | `build`
- 예시:
  ```
  [0-1] bench: Add PerfScope IDisposable struct for unified instrumentation
  [1-4] perf: Implement differential update in LoadEmailsAsync
  [4-1] perceived: Add body loading overlay in detail panel
  ```

### 2. 변경 최소화
- 커밋당 변경 파일 ≤ **5개**
- 커밋당 변경 행 ≤ **200행** (신규 파일 예외)
- **성능 목적 외 기능 변경 절대 금지**
- 기존 동작 보존: 입출력 동일, API 시그니처 호환

### 3. 리팩토링 범위 제한
- 현재 Task 범위 내에서만 수정
- "보이는 김에 고치기" 금지
- 기존 테스트 깨지면 같은 커밋에서 수정

### 4. COM 제약 존중
- STA 스레드에서만 COM 호출
- `InvokeAsync` 경유 필수
- 배치 메서드도 단일 `InvokeAsync` 내 foreach

---

## 테스트 게이트

### 매 커밋 직전 실행

```bash
# 1단계: 빌드
dotnet build MailTriageAssistant/MailTriageAssistant.csproj

# 2단계: 전체 테스트
dotnet test MailTriageAssistant.Tests/

# 3단계 (Regex 변경 시): 보안 테스트
dotnet test MailTriageAssistant.Tests/ --filter "FullyQualifiedName~Redaction"
```

### 게이트 통과 조건
- `dotnet build`: Exit code 0
- `dotnet test`: Exit code 0, 전체 Green
- PII 보안 테스트 (`RedactionServiceTests`, `RedactionSecurityTests`): **무조건 Green**

---

## 실패 처리

### 빌드 실패
```
1회차: 에러 분석 → 수정 시도
2회차: 실패 지속 → git revert HEAD → 블로커 리포트
```

### 테스트 실패
```
1회차: 실패 테스트 분석 → 수정 시도
2회차: 실패 지속 → git revert HEAD → 블로커 리포트
```

### 성능 회귀
```
벤치 수치가 이전 대비 20% 이상 악화:
→ git revert HEAD → 재설계
```

### 블로커 리포트 생성

파일: `.ai/reports/2026-02-13_codex_blockers.md`

```markdown
# Codex Perf Blocker Report
> Date: 2026-02-13

## Blocked Task
- Phase: {N}
- Commit: [N-N] {타입}: {설명}
- 파일: {경로}
- 에러: {메시지}

## 시도한 수정
1. {내용}
2. {내용}

## 의심 원인
- {추정}

## 성능 지표 (적용 가능한 경우)
- Before: {ms}
- After: {ms}
- 악화: {%}
```

---

## 변경 로그

파일: `.ai/reports/2026-02-13_codex_change_log.md`

매 커밋 후 추가:

```markdown
## [N-N] 타입: 설명
- **Status**: ✅ Committed / ❌ Reverted / ⏸️ Skipped
- **Files**: file1.cs, file2.xaml
- **Lines**: +N / -N
- **Build**: ✅ / ❌
- **Test**: ✅ (N/N) / ❌ (N/N)
- **Perf Before**: {metric}={value}
- **Perf After**: {metric}={value}
- **Notes**: {특이사항}
```

---

## 보안 불변 규칙 (성능 최적화 중에도 위반 금지)

위반 시 즉시 `git revert HEAD`

1. ❌ 이메일 본문(Body)을 디스크 파일에 저장
2. ❌ 이메일 본문을 `Console`/`Debug`/`Trace`/`ILogger`로 출력
3. ❌ `ex.Message`를 UI에 직접 표시
4. ❌ PII를 마스킹 없이 클립보드에 복사
5. ❌ 외부 API 호출
6. ❌ 로그에 발신자 이메일 원본 포함
7. ❌ `[GeneratedRegex]` 전환 시 PII 마스킹 패턴 변경
8. ❌ Perf Budget 상한 초과 커밋

---

## 실행 순서 요약

```
Phase 0: [0-1] → [0-2] → [0-3] → [0-4] → [0-5] → [0-6] → [0-7]  ← 계측
Phase 1: [1-1] → [1-2] → [1-3] → [1-4]                              ← 데이터 파이프라인
Phase 2: [2-1] → [2-2] → [2-3]                                        ← 렌더링
Phase 3: [3-1] → [3-2] → [3-3]                                        ← Regex + 빌드
Phase 4: [4-1] → [4-2] → [4-3] → [4-4] → [4-5]                    ← 체감속도
Phase 5: [5-1] → [5-2] → [5-3]                                        ← 안정성
Phase 6: [6-1] → [6-2]                                                  ← 검증
```

총 **28 commits**, Phase별 DoD 충족 시에만 다음 Phase 진행.

---

## 핵심 최적화 효과 요약

| 최적화 | 대상 | 예상 효과 | Phase |
|---|---|---|---|
| 차분 업데이트 | 자동 분류 시 프리페치 | 1-2.5s 절감 | 1-4 |
| 배치 GetBody | 프리페치 InvokeAsync 오버헤드 | 30-90ms 절감 | 1-1~3 |
| RedactionConverter 제거 | 목록 렌더 Regex | 50-100ms/렌더 절감 | 2-1~2 |
| GeneratedRegex | Redact Regex JIT | JIT 비용 제거 | 3-1 |
| ReadyToRun | 앱 시작 JIT | 200-500ms 시작 단축 | 3-3 |
| SplashScreen | 앱 시작 체감 | 즉각 표시 | 4-4 |
| PerfScope 통합 | 계측 코드 표준화 | 일관된 지표 수집 | 0-1 |
