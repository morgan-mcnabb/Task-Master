using FluentValidation;
using TaskMasterApi.Contracts.Tasks;

namespace TaskMasterApi.Validation.Tasks;

public sealed class TaskQueryRequestValidator : AbstractValidator<TaskQueryRequest>
{
    public TaskQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100);

        When(x => x.DueOnOrBefore.HasValue && x.DueOnOrAfter.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.DueOnOrBefore!.Value >= x.DueOnOrAfter!.Value)
                .WithMessage("DueOnOrBefore must be on or after DueOnOrAfter.");
        });

        When(x => x.Tags is not null, () =>
        {
            RuleForEach(x => x.Tags!)
                .Must(name => !string.IsNullOrWhiteSpace(name?.Trim()))
                .WithMessage("Tag names in filter cannot be blank.");
        });

        RuleFor(x => x.SortBy)
            .IsInEnum();

        RuleFor(x => x.SortDirection)
            .IsInEnum();
    }
}