using Core.Application.Identity.Common;
using Core.Application.Identity.DTOs.User;
using FluentValidation;

namespace Core.Application.Identity.Validators;

public sealed class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(IdentityMessages.PhoneRequired)
            .Matches(@"^09\d{9}$").WithMessage(IdentityMessages.InvalidPhoneFormat);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(IdentityMessages.InvalidEmailFormat)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(IdentityMessages.FirstNameRequired)
            .MaximumLength(100).WithMessage(IdentityMessages.FirstNameMaxLength);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(IdentityMessages.LastNameRequired)
            .MaximumLength(100).WithMessage(IdentityMessages.LastNameMaxLength);

        RuleFor(x => x.NationalCode)
            .Length(10).WithMessage(IdentityMessages.NationalCodeLength)
            .Matches(@"^\d{10}$").WithMessage(IdentityMessages.NationalCodeDigitsOnly)
            .When(x => !string.IsNullOrWhiteSpace(x.NationalCode));

        RuleFor(x => x.Password)
            .Must(IsAcceptablePassword).WithMessage(IdentityMessages.PasswordTooWeak)
            .When(x => !string.IsNullOrWhiteSpace(x.Password));
    }

    internal static bool IsAcceptablePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 64)
            return false;
        return password.Any(char.IsLetter) && password.Any(char.IsDigit);
    }
}
