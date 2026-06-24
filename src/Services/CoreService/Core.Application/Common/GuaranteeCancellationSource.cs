using Core.Application.DTOs;
using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Common;

public static class GuaranteeCancellationSource
{
    public static string ResolveReference(GuaranteeCase entity)
        => entity.CaseNumber;

    public static bool RequiresSettlementConfirmation(GuaranteeCase entity)
        => (entity.ApprovalForm?.ActiveCommitments ?? 0m) > 0m;

    public static bool HasIssuanceDocuments(GuaranteeCase entity)
        => entity.Documents.Any(d => !d.IsDeleted && d.DocumentType == GuaranteeDocumentType.GuaranteeInstrument);

    public static GuaranteeSourceSnapshotDto BuildSnapshot(GuaranteeCase entity)
    {
        var app = entity.Application;
        var form = entity.ApprovalForm;
        var instrument = entity.Documents
            .Where(d => !d.IsDeleted && d.DocumentType == GuaranteeDocumentType.GuaranteeInstrument)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault();

        return new GuaranteeSourceSnapshotDto(
            entity.Id,
            entity.CaseNumber,
            form?.GuaranteeAmount ?? app?.RequestedGuaranteeAmount,
            form?.Beneficiary ?? app?.BeneficiaryName,
            form?.IssuanceDate,
            form?.ExpiryDate ?? app?.ValidityTo,
            form?.CommissionAmount,
            form?.DepositAmount,
            form?.ActiveCommitments,
            RequiresSettlementConfirmation(entity),
            instrument is not null,
            instrument?.FileName);
    }
}
