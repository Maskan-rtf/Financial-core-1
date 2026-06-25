using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Domain.Entities.Guarantee;

public sealed class GuaranteeCaseComment : Entity<Guid>, IAuditableEntity
{
    private GuaranteeCaseComment()
    {
        SenderUserId = default!;
        Message = default!;
    }

    public GuaranteeCaseComment(
        Guid caseId,
        GuaranteeCasePhase phase,
        string senderUserId,
        string? senderRole,
        string message,
        bool isRevisionRequest,
        bool isInternal,
        Guid? parentId = null,
        GuaranteeCaseStatus? workflowStatusAtCreation = null)
    {
        Id = Guid.NewGuid();
        CaseId = caseId;
        Phase = phase;
        SenderUserId = senderUserId;
        SenderRole = senderRole;
        Message = message;
        IsRevisionRequest = isRevisionRequest;
        IsInternal = isInternal;
        ParentId = parentId;
        WorkflowStatusAtCreation = workflowStatusAtCreation;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CaseId { get; private set; }
    public GuaranteeCase Case { get; private set; } = default!;

    public GuaranteeCasePhase Phase { get; private set; }
    public string SenderUserId { get; private set; }
    public string? SenderRole { get; private set; }
    public string Message { get; private set; }
    public bool IsRevisionRequest { get; private set; }
    public bool IsInternal { get; private set; }
    public GuaranteeCaseStatus? WorkflowStatusAtCreation { get; private set; }

    public Guid? ParentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
}
