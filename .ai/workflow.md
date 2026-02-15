# MailTriageAssistant — 1-사이클 워크플로우

> 리뷰 → 설계 → Codex 구현 → 테스트 → 회귀 점검 → 정리

---

## Phase 1: 리뷰 (Review)

### 실행 에이전트
`01_code_review`, `02_uiux`, `05_perf_reliability`, `06_security_privacy` 를 **병렬** 실행

### 산출물
| 에이전트 | 산출물 |
|---|---|
| 01 Code Review | `.ai/reports/YYYY-MM-DD_code_review.md` |
| 02 UI/UX | `.ai/reports/YYYY-MM-DD_uiux.md` |
| 05 Perf & Reliability | `.ai/reports/YYYY-MM-DD_perf_reliability.md` |
| 06 Security & Privacy | `.ai/reports/YYYY-MM-DD_security_privacy.md` |

### 완료 조건 (Definition of Done)
- [ ] 모든 리포트에 위험도 분류(`Critical/Major/Minor/Info`)가 포함됨
- [ ] 각 리포트에 발견 항목 개수와 파일/라인 참조가 명시됨
- [ ] Codex Handoff 섹션이 작성됨 (구현 가능한 작업 목록)

---

## Phase 2: 설계 (Design)

### 실행 에이전트
`04_feature_discovery` 를 실행하여 새 기능/개선 아이디어 발굴

### 산출물
| 에이전트 | 산출물 |
|---|---|
| 04 Feature Discovery | `.ai/reports/YYYY-MM-DD_feature_discovery.md` |

### 추가 작업
1. Phase 1 리포트들에서 `Critical` + `Major` 항목을 취합
2. 우선순위 기반 **통합 백로그** 작성: `.ai/reports/YYYY-MM-DD_backlog.md`
3. 각 백로그 항목에 예상 난이도(S/M/L), 의존성, 담당 에이전트 배정

### 완료 조건
- [ ] 통합 백로그에 최소 `Critical` 항목 전부 포함
- [ ] 각 항목에 수정 대상 파일/함수가 명시됨
- [ ] 변경 설계 문서가 존재 (큰 변경의 경우)

---

## Phase 3: Codex 구현 (Implementation)

### 실행 에이전트
`03_test_engineering` 먼저 → 그 다음 백로그 항목별 Codex 구현

### 단계
1. **테스트 프로젝트 생성** (`MailTriageAssistant.Tests`)
2. **기존 서비스 단위 테스트 작성** (RedactionService, TriageService, DigestService)
3. **백로그 항목을 1개씩 구현** (커밋 단위 원칙 준수)
4. **각 커밋 후 `dotnet build` 확인**

### 산출물
| 항목 | 산출물 |
|---|---|
| 테스트 계획 | `.ai/reports/YYYY-MM-DD_test_engineering.md` |
| 변경 로그 | 각 커밋의 변경 사항 기록 |
| PR 요약 | 변경 내용 1줄 요약 |

### 완료 조건
- [ ] `dotnet build` 성공 (경고 0)
- [ ] 단위 테스트 전체 통과 (`dotnet test`)
- [ ] Critical 항목 0건 잔여

---

## Phase 4: 테스트 (Verification)

### 실행
```bash
dotnet build
dotnet test
```

### 수동 검증 체크리스트
- [ ] Classic Outlook에서 "Run Triage Now" 클릭 → 이메일 목록 표시
- [ ] 이메일 선택 → 마스킹된 본문 표시 (원본 노출 없음)
- [ ] "Digest 생성 & Teams 전송" → Teams 열림 or 폴백 MessageBox
- [ ] 30초 후 클립보드 비워짐
- [ ] New Outlook 실행 시 에러 메시지 출력

### 완료 조건
- [ ] 모든 자동 테스트 통과
- [ ] 수동 검증 체크리스트 전체 확인
- [ ] Output 창에 이메일 본문 미노출 확인

---

## Phase 5: 회귀 점검 (Regression Check)

### 실행
1. `06_security_privacy` 에이전트 재실행 — 구현 후 새로운 보안 취약점 확인
2. `05_perf_reliability` 에이전트 재실행 — 성능 저하 여부 확인
3. `01_code_review` 에이전트 재실행 — 새 코드에 대한 품질 점검

### 완료 조건
- [ ] 새 Critical 항목 0건
- [ ] 이전 리포트 대비 Major 항목 증가 없음
- [ ] `dotnet build` + `dotnet test` 통과

---

## Phase 6: 정리 (Wrap-up)

### 산출물
1. **최종 통합 리포트:** `.ai/reports/YYYY-MM-DD_cycle_summary.md`
   - 발견 항목 총 개수
   - 수정 완료 항목
   - 잔여 백로그
   - 다음 사이클 권장 사항
2. **사양서 업데이트:** 변경된 기능/제약 사항 반영
3. **백로그 갱신:** 미완료 항목 + 새 발견 항목 통합

### 완료 조건
- [ ] 최종 리포트 작성 완료
- [ ] 백로그에 잔여 항목 우선순위 재배정
- [ ] 사이클 종료 선언
