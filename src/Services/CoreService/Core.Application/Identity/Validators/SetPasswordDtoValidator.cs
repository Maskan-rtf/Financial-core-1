using Core.Application.Identity.Common;
using Core.Application.Identity.DTOs.User;
using FluentValidation;

namespace Core.Application.Identity.Validators;

public sealed class SetPasswordDtoValidator : AbstractValidator<SetPasswordDto>
{
    public SetPasswordDtoValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(IdentityMessages.PasswordRequired)
            .Must(CreateUserDtoValidator.IsAcceptablePassword!).WithMessage(IdentityMessages.PasswordTooWeak);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage(IdentityMessages.PasswordConfirmMismatch);
    }
}
