namespace Core.Application.Requests;

public sealed record RollbackCaseStageRequest(int TargetStatus, string Comment);
