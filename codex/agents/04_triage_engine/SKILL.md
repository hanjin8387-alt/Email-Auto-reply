---
name: Triage Engine Agent
description: 이메일 분류 및 우선순위 점수 산출 엔진 구현
---

# Agent 04: Triage Engine

## 역할
이메일의 발신자, 제목, 본문(선택)을 분석하여 카테고리와 우선순위 점수를 산출합니다.

## 핵심 설계: 2단계 분석
| 단계 | 메서드 | 입력 | 용도 |
|---|---|---|---|
| Phase 1 | `AnalyzeHeader()` | 발신자 + 제목 | 목록 로드 시 예비 분류 (빠름) |
| Phase 2 | `AnalyzeWithBody()` | 발신자 + 제목 + 본문 | 선택/요약 시 정밀 분석 |

## 생성 파일

### `Services/TriageService.cs`

#### 점수 산출 로직
```
BaseScore = 50 (기본)

가산:
  + 30  VIP 발신자 (사전 정의 리스트 매칭)
  + 20  Action 키워드 포함: "요청", "승인", "긴급", "ASAP", "기한", "Due"
  + 15  Approval 키워드: "결재", "상신", "승인요청"
  + 10  Meeting 키워드: "초대", "Invite", "회의", "미팅", "Zoom", "Teams"

감산:
  - 50  Newsletter 키워드: "구독", "광고", "No-Reply", "News", "Unsubscribe"
  - 10  Unknown 발신자 (VIP도 Newsletter도 아닌 경우)
  
범위: 0 ~ 100 (clamp)
```

#### 카테고리 매핑
```csharp
// 우선순위 순서 (먼저 매칭된 것이 할당)
1. Action  → "요청", "승인", "긴급", "ASAP", "기한", "Due"
2. Approval → "결재", "상신", "승인요청"
3. VIP     → VIP 리스트의 발신자
4. Meeting → "초대", "Invite", "회의", "미팅", "Zoom", "Teams"
5. Newsletter → "구독", "광고", "No-Reply", "News"
6. FYI     → "참고", "공유", "FYI", "공지"
7. Other   → 위 어디에도 해당하지 않는 경우
```

#### VIP 리스트 (시뮬레이션)
```csharp
private static readonly HashSet<string> VipSenders = new(StringComparer.OrdinalIgnoreCase)
{
    "ceo@company.com",
    "cto@company.com",
    "manager@company.com",
    // 실제로는 설정 파일에서 로드
};
```

#### ActionHint 생성
| 카테고리 | ActionHint |
|---|---|
| Action | "즉시 처리 필요" |
| Approval | "결재 확인 필요" |
| VIP | "VIP 응답 필요" |
| Meeting | "일정 확인 필요" |
| Newsletter | "구독 해제 고려" |
| FYI | "읽기 전용" |
| Other | "검토" |

## 완료 기준
- "긴급 요청" 제목 → Category=Action, Score ≥ 70
- VIP 이메일 → Category=VIP, Score ≥ 80
- 뉴스레터 이메일 → Category=Newsletter, Score ≤ 10
- Phase 1과 Phase 2의 결과 구조가 동일
