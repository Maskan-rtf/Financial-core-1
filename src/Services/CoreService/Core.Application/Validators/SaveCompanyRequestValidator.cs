using FluentValidation;
using Core.Application.Requests;

namespace Core.Application.Validators;

public sealed class SaveCompanyRequestValidator : AbstractValidator<SaveCompanyRequest>
{
    public SaveCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.EconomicCode).MaximumLength(32);
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NationalId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.City).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Province).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.LegalType).IsInEnum();
        RuleFor(x => x.TechnologyType).IsInEnum();
        RuleFor(x => x.TechnologyDomain).IsInEnum();
        RuleFor(x => x.RepresentativeName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.RepresentativePhone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.CeoName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CeoPhone).NotEmpty().MaximumLength(32);
    }
}
