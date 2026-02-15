---
name: Project Setup Agent
description: .NET 8 WPF 프로젝트 초기화, csproj 구성, 데이터 모델 생성
---

# Agent 01: Project Setup

## 역할
프로젝트 스캐폴딩과 데이터 모델을 생성합니다. 다른 모든 에이전트 전에 먼저 실행되어야 합니다.

## 입력
- 없음 (최초 실행)

## 실행 단계

### Step 1: 프로젝트 생성
```bash
dotnet new wpf -n MailTriageAssistant
cd MailTriageAssistant
dotnet add package NetOfficeFw.Outlook
```

### Step 2: csproj 수정
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
```

### Step 3: 폴더 구조 생성
```
Models/, Services/, ViewModels/, Helpers/
```

### Step 4: 데이터 모델 파일 생성

#### `Models/EmailCategory.cs`
```csharp
public enum EmailCategory
{
    Action, VIP, Meeting, Approval, FYI, Newsletter, Other
}
```

#### `Models/RawEmailHeader.cs`
```csharp
public class RawEmailHeader
{
    public string EntryId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedTime { get; set; }
    public bool HasAttachments { get; set; }
}
```

#### `Models/AnalyzedItem.cs`
- `INotifyPropertyChanged` 구현
- Properties: EntryId, Sender, Subject, ReceivedTime, Category (EmailCategory), Score (int), RedactedSummary (string, 변경알림), ActionHint, Tags (string[])

#### `Models/ReplyTemplate.cs`
```csharp
public class ReplyTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BodyContent { get; set; } = string.Empty;
}
```

## 출력 파일
- `MailTriageAssistant.csproj`
- `Models/EmailCategory.cs`
- `Models/RawEmailHeader.cs`
- `Models/AnalyzedItem.cs`
- `Models/ReplyTemplate.cs`

## 완료 기준
- `dotnet build` 성공
- 모든 모델 클래스 컴파일 완료
