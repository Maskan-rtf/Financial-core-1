using FluentValidation;
using Core.Application.Requests;
using Core.Domain.Enums;

namespace Core.Application.Validators;

public sealed class CreateInvestmentCaseRequestValidator : AbstractValidator<CreateInvestmentCaseRequest>
{
    public CreateInvestmentCaseRequestValidator()
    {
        RuleFor(x => x.ApplicantType).IsInEnum();
        RuleFor(x => x.Title).MaximumLength(256);

        When(x => x.ApplicantType == ApplicantType.Company, () =>
        {
            RuleFor(x => x.CompanyId).NotEmpty();
        });
    }
}
