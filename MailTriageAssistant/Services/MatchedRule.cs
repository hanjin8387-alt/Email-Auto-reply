namespace MailTriageAssistant.Services;

public sealed record MatchedRule(string RuleCode, int ScoreDelta, string Reason);
