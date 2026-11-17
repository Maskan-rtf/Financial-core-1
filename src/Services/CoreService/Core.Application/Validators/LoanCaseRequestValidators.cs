using Core.Application.Requests;
using FluentValidation;

namespace Core.Application.Validators;

public sealed class CreateLoanCaseRequestValidator : AbstractValidator<CreateLoanCaseRequest>
{
    public CreateLoanCaseRequestValidator()
    {
        RuleFor(x => x.ApplicantType).IsInEnum();
        RuleFor(x => x.CompanyId)
            .NotEmpty()
            .When(x => x.ApplicantType == Core.Domain.Enums.ApplicantType.Company);
    }
}

public sealed class UpdateLoanApplicationRequestValidator : AbstractValidator<UpdateLoanApplicationRequest>
{
    public UpdateLoanApplicationRequestValidator()
    {
        RuleFor(x => x.RequestedAmount).GreaterThan(0).When(x => x.RequestedAmount.HasValue);
        RuleFor(x => x.FacilitySubject).MaximumLength(2000);
        RuleFor(x => x.OfferedGuarantees).MaximumLength(4000);
        RuleFor(x => x.ApplicantCategoryOther).MaximumLength(512);
        RuleFor(x => x.RepresentativePosition).MaximumLength(256);
    }
}

public sealed class RegisterLoanPaymentRequestValidator : AbstractValidator<RegisterLoanPaymentRequest>
{
    public RegisterLoanPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionNumber).NotEmpty().MaximumLength(128);
        RuleFor(x => x.StageNumber).GreaterThan(0);
    }
}

public sealed class MarkLoanInstallmentPaidRequestValidator : AbstractValidator<MarkLoanInstallmentPaidRequest>
{
    public MarkLoanInstallmentPaidRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionNumber).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpsertLoanDisbursementPlanRequestValidator : AbstractValidator<UpsertLoanDisbursementPlanRequest>
{
    public UpsertLoanDisbursementPlanRequestValidator()
    {
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.StageNumber).InclusiveBetween(1, 9999);
            item.RuleFor(x => x.Amount).GreaterThan(0);
            item.RuleFor(x => x.Notes).MaximumLength(2000);
        });
    }
}
