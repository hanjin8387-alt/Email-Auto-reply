---
name: WPF UI Agent
description: WPF 대시보드 UI, MainViewModel (MVVM), 컨버터 구현
---

# Agent 06: WPF UI

## 역할
MVVM 패턴의 WPF 대시보드를 구현합니다. 3단계 데이터 로딩을 처리하는 ViewModel과 시각 요소를 생성합니다.

## 의존성
- `Models/AnalyzedItem.cs` (Agent 01)
- `Services/IOutlookService.cs` (Agent 02)
- `Services/RedactionService.cs` (Agent 03)
- `Services/TriageService.cs` (Agent 04)
- `Services/DigestService.cs` (Agent 05)

## 생성 파일

### 1. `ViewModels/RelayCommand.cs`
```csharp
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    
    // 표준 ICommand 구현
    // event CanExecuteChanged: CommandManager.RequerySuggested 연결
}
```
- 동기/비동기 실행 모두 지원
- `AsyncRelayCommand`도 같이 구현 (async 작업용)

### 2. `ViewModels/MainViewModel.cs`

#### 프로퍼티
```csharp
public ObservableCollection<AnalyzedItem> Emails { get; }
public AnalyzedItem? SelectedEmail { get; set; }  // 변경 알림 포함
public string StatusMessage { get; set; }          // "대기 중", "처리 중..." 등
public bool IsLoading { get; set; }                // 로딩 인디케이터
public List<ReplyTemplate> Templates { get; }      // 템플릿 목록
public ReplyTemplate? SelectedTemplate { get; set; }
```

#### 3단계 로딩 로직

**Phase 1 — LoadEmailsCommand (비동기):**
```
1. IsLoading = true
2. OutlookService.FetchInboxHeaders() 호출
3. 각 헤더에 대해 TriageService.AnalyzeHeader(sender, subject)
4. AnalyzedItem 생성 (RedactedSummary = "선택하면 본문을 불러옵니다...")
5. Score 내림차순으로 정렬 후 Emails에 추가
6. IsLoading = false
```

**Phase 2 — SelectedEmail 변경 시:**
```
1. 이미 본문이 캐시되어 있으면 Skip
2. OutlookService.GetBody(EntryId) 호출
3. RedactionService.Redact() 적용
4. AnalyzedItem.RedactedSummary 업데이트
5. TriageService.AnalyzeWithBody()로 점수 재산출
```

**Phase 3 — GenerateDigestCommand:**
```
1. 상위 10개 항목 필터링
2. 본문 미로드 항목만 일괄 GetBody() + Redact()
3. DigestService.GenerateDigest() 호출
4. DigestService.OpenTeams() 호출
```

#### COMException 처리
```csharp
catch (COMException)
{
    StatusMessage = "Outlook에 연결할 수 없습니다. Outlook을 시작해 주세요.";
}
catch (NotSupportedException ex)
{
    StatusMessage = ex.Message; // "Classic Outlook이 필요합니다"
}
```

### 3. `Helpers/ScoreToColorConverter.cs`
```csharp
public class ScoreToColorConverter : IValueConverter
{
    // Score ≥ 80 → #FF4444 (빨강)
    // Score ≥ 50 → #FFAA00 (주황)
    // Score ≥ 30 → #44AA44 (초록)
    // Score <  30 → #888888 (회색)
}
```

### 4. `MainWindow.xaml`
```xml
<!-- 2컬럼 레이아웃 -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="2*"/>  <!-- 왼쪽: 이메일 목록 -->
        <ColumnDefinition Width="3*"/>  <!-- 오른쪽: 상세 보기 -->
    </Grid.ColumnDefinitions>
    
    <!-- 왼쪽 패널 -->
    <!-- ListBox with ItemTemplate: Score(색상), Sender, Subject -->
    <!-- StatusBar: StatusMessage 바인딩 -->
    
    <!-- 오른쪽 패널 -->
    <!-- TextBlock: RedactedSummary (Redacted body) -->
    <!-- ComboBox: Template 선택 -->
    <!-- Buttons: "Outlook에서 열기", "Copilot용 복사", "템플릿으로 답장" -->
    <!-- Button: "Digest 생성 & Teams 전송" (하단) -->
</Grid>
```

### 5. `MainWindow.xaml.cs`
- `DataContext = new MainViewModel(...)` 설정
- 최소한의 코드비하인드 (MVVM 원칙)

## 완료 기준
- 이메일 리스트 Score 색상 표시
- 항목 선택 시 마스킹된 본문 표시
- "Digest 생성" 클릭 시 Teams 연동
- Outlook 미연결 시 상태 메시지 표시
