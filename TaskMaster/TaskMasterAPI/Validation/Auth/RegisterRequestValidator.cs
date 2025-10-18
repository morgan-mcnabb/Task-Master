using FluentValidation;
using TaskMasterApi.Contracts.Auth;

namespace TaskMasterApi.Validation.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(256);

        // Align with Identity options (min length 8, digit/lowercase enforced by Identity itself)
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}