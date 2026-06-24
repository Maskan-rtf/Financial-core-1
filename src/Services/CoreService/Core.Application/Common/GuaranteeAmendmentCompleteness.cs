using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Common;

public static class GuaranteeAmendmentCompleteness
{
    public static bool IsComplete(GuaranteeCase caseEntity)
    {
        if (!caseEntity.AmendmentType.HasValue)
            return false;

        return caseEntity.AmendmentType.Value switch
        {
            AmendmentType.Extension => caseEntity.AmendmentRequestedValidityTo.HasValue
                && !string.IsNullOrWhiteSpace(caseEntity.AmendmentReason),
            AmendmentType.Reduction => caseEntity.AmendmentRequestedAmount is > 0
                && !string.IsNullOrWhiteSpace(caseEntity.AmendmentReason),
            AmendmentType.Cancellation => !string.IsNullOrWhiteSpace(caseEntity.AmendmentReason),
            _ => false
        };
    }
}
