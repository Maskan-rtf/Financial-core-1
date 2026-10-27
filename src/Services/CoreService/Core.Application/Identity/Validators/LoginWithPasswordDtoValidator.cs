using Core.Application.Identity.Common;
using Core.Application.Identity.DTOs.User;
using FluentValidation;

namespace Core.Application.Identity.Validators;

public sealed class LoginWithPasswordDtoValidator : AbstractValidator<LoginWithPasswordDto>
{
    public LoginWithPasswordDtoValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(IdentityMessages.PhoneRequired)
            .Matches(@"^09\d{9}$").WithMessage(IdentityMessages.InvalidPhoneFormat);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(IdentityMessages.PasswordRequired);
    }
}
