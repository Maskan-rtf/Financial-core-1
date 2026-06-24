using Core.Application.Common;
using Core.Application.Requests;
using FluentValidation;

namespace Core.Application.Validators;

public sealed class RollbackCaseStageRequestValidator : AbstractValidator<RollbackCaseStageRequest>
{
    public RollbackCaseStageRequestValidator()
    {
        RuleFor(x => x.TargetStatus)
            .GreaterThan(0)
            .WithMessage(ApiMessages.InvalidTargetStage);

        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage(ApiMessages.CaseStageRollbackCommentRequired);
    }
}
