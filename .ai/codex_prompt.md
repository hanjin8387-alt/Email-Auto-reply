# Codex 실행 프롬프트 — MailTriageAssistant 성능 최적화

아래 파일들을 순서대로 읽고, Master Plan의 Phase 0부터 Phase 6까지 1 commit = 1 task 규칙으로 코드를 수정하라.

---

## 필수 읽기 파일 (이 순서대로 읽을 것)

1. **`.ai/CODEX_Skill.md`** — 실행 규칙 전문. 커밋 규칙, 테스트 게이트, 실패 처리, 보안 불변 규칙을 포함한다. **이 파일의 모든 규칙을 반드시 준수하라.**
2. **`.ai/plans/2026-02-13_perf_master_plan.md`** — 6 Phase, 28 commits 실행 계획. 각 Phase의 커밋 목록, 변경 파일, 변경 요지, 테스트 커맨드, 수용 기준, 위험도가 정의되어 있다.
3. **`.ai/perf_cycle_workflow.md`** — 성능 최적화 사이클 워크플로우. 베이스라인 정의 → 측정 → 병목 식별 → 개선 설계 → 구현 → 회귀 검증 → 정리.

## 참고 리포트 (각 에이전트 분석 결과 — 구현 시 상세 근거로 참조)

- `.ai/reports/2026-02-13_01_profiling_benchmark.md` — 계측 누락 포인트, 베이스라인 지표
- `.ai/reports/2026-02-13_02_frontend_rendering.md` — RedactionConverter Regex 100회, Refresh 중복
- `.ai/reports/2026-02-13_03_backend_latency.md` — 배치 GetBody, GeneratedRegex, 프리페치 순차성
- `.ai/reports/2026-02-13_04_network_cache_data.md` — 차분 업데이트, 프리페치 Task 조율, 헤더 TTL
- `.ai/reports/2026-02-13_05_build_bundle_size.md` — PDB 제거, ReadyToRun, TrimMode
- `.ai/reports/2026-02-13_06_perceived_speed_ux.md` — 스플래시, 본문 오버레이, 선택 복원, 카운트다운
- `.ai/reports/2026-02-13_07_perf_reliability.md` — IOptionsMonitor 릭, Perf Budget, 메모리 측정
- `.ai/reports/2026-02-13_08_observability_metrics.md` — PerfScope 헬퍼, perf_metrics.json, ETW 확장

---

## 실행 절차 (반드시 이 순서를 따를 것)

### Phase 진행
1. **CODEX_Skill.md를 먼저 읽고** 모든 규칙을 숙지하라.
2. **perf_master_plan.md의 Phase 0, Commit [0-1]**부터 시작하라.
3. 각 Phase 내 커밋을 순서대로 실행하라 (건너뛰기 금지).
4. Phase의 DoD(Definition of Done)를 충족해야 다음 Phase로 진행한다.

### 각 커밋 실행 절차
```
1. Master Plan 테이블에서 해당 커밋의 [변경 파일], [변경 요지] 확인
2. 해당 리포트 파일에서 상세 근거 확인 (파일:줄, 이슈, 권장사항)
3. 코드 수정 (변경 파일 ≤ 5개, 변경 행 ≤ 200행)
4. 빌드 검증:
   dotnet build MailTriageAssistant/MailTriageAssistant.csproj
5. 테스트 검증:
   dotnet test MailTriageAssistant.Tests/
6. (Regex 변경 시) 보안 테스트:
   dotnet test MailTriageAssistant.Tests/ --filter "FullyQualifiedName~Redaction"
7. 성공 시 커밋:
   git add -A && git commit -m "[Phase-Commit] 타입: 설명"
8. 변경 로그 추가:
   .ai/reports/2026-02-13_codex_change_log.md에 결과 기록
```

### 실패 처리
```
빌드/테스트 1회 실패 → 에러 분석 → 수정 시도
빌드/테스트 2회 연속 실패 → git revert HEAD → .ai/reports/2026-02-13_codex_blockers.md 작성 → 해당 Task 스킵 → 다음 Task
성능 회귀 20% 이상 → git revert HEAD → 재설계
```

---

## 커밋 메시지 형식

```
[Phase-Commit] 타입: 한줄설명(영어)
```

타입: `perf` | `perceived` | `bench` | `build`

예시:
```
[0-1] bench: Add PerfScope IDisposable struct for unified instrumentation
[1-4] perf: Implement differential update in LoadEmailsAsync
[2-1] perf: Add RedactedSender/RedactedSubject pre-computed properties
[4-1] perceived: Add body loading overlay in detail panel
```

---

## 보안 불변 규칙 (절대 위반 금지 — 위반 시 즉시 revert)

1. ❌ 이메일 본문(Body)을 디스크 파일에 저장
2. ❌ 이메일 본문을 Console/Debug/Trace/ILogger로 출력
3. ❌ ex.Message를 UI에 직접 표시
4. ❌ PII를 마스킹 없이 클립보드에 복사
5. ❌ 외부 API 호출
6. ❌ 로그에 발신자 이메일 원본 포함
7. ❌ [GeneratedRegex] 전환 시 PII 마스킹 패턴 변경 (보안 테스트 필수)
8. ❌ 성능 목적 외 기능 변경

---

## 저장소 정보

| 항목 | 값 |
|---|---|
| 언어 | C# (.NET 8) |
| 프레임워크 | WPF |
| 아키텍처 | MVVM |
| 프로젝트 | `MailTriageAssistant/MailTriageAssistant.csproj` |
| 테스트 | `MailTriageAssistant.Tests/` (xUnit + Moq + FluentAssertions) |
| 빌드 | `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` |
| 테스트 | `dotnet test MailTriageAssistant.Tests/` |

---

## 시작하라.

Phase 0, Commit [0-1]부터 실행하라. CODEX_Skill.md를 먼저 읽어라.
