using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Domain.Entities.Investment;

public sealed class InvestmentCaseComment : Entity<Guid>, IAuditableEntity
{
    private InvestmentCaseComment()
    {
        SenderUserId = default!;
        Message = default!;
    }

    public InvestmentCaseComment(
        Guid caseId,
        CasePhase phase,
        string senderUserId,
        string? senderRole,
        string message,
        bool isRevisionRequest,
        bool isInternal,
        Guid? parentId = null,
        CaseStatus? workflowStatusAtCreation = null)
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
    public InvestmentCase Case { get; private set; } = default!;

    public CasePhase Phase { get; private set; }
    public string SenderUserId { get; private set; }
    public string? SenderRole { get; private set; }
    public string Message { get; private set; }
    public bool IsRevisionRequest { get; private set; }
    public bool IsInternal { get; private set; }
    public CaseStatus? WorkflowStatusAtCreation { get; private set; }

    public Guid? ParentId { get; private set; }
    public InvestmentCaseComment? Parent { get; private set; }
    public List<InvestmentCaseComment> Replies { get; private set; } = [];
    public List<InvestmentCaseCommentAttachment> Attachments { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void AddAttachment(string s3Key, string fileName)
    {
        Attachments.Add(new InvestmentCaseCommentAttachment(Id, s3Key, fileName));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

