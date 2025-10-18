using FluentValidation;
using TaskMasterApi.Contracts.Auth;

namespace TaskMasterApi.Validation.Auth;

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    private const int KeyMaxLength = 128;
    private const int ValueMaxLength = 2048;

    public UpdateSettingsRequestValidator()
    {
        // Null means "clear settings" in current controller, which is valid.
        When(x => x.Settings is not null, () =>
        {
            RuleForEach(x => x.Settings!)
                .ChildRules(dict =>
                {
                    dict.RuleFor(kv => kv.Key)
                        .NotEmpty().WithMessage("Setting key is required.")
                        .MaximumLength(KeyMaxLength);

                    dict.RuleFor(kv => kv.Value)
                        .NotNull().WithMessage("Setting value is required.")
                        .MaximumLength(ValueMaxLength);
                });

            RuleFor(x => x.Settings!)
                .Must(d => d.Keys
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == d.Count)
                .WithMessage("Setting keys must be unique (case-insensitive).");
        });
    }
}