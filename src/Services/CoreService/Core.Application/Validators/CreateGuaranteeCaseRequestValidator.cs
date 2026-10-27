using Core.Application.Requests;
using Core.Domain.Enums;
using FluentValidation;

namespace Core.Application.Validators;

public sealed class CreateGuaranteeCaseRequestValidator : AbstractValidator<CreateGuaranteeCaseRequest>
{
    public CreateGuaranteeCaseRequestValidator()
    {
        RuleFor(x => x.ApplicantType).IsInEnum();
        RuleFor(x => x.CompanyId)
            .NotEmpty()
            .When(x => x.ApplicantType == ApplicantType.Company);
    }
}
