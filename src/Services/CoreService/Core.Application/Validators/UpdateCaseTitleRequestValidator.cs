using FluentValidation;

namespace Core.Application.Validators;

public sealed class UpdateCaseTitleRequestValidator : AbstractValidator<Requests.UpdateCaseTitleRequest>
{
    public UpdateCaseTitleRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(256);
    }
}
