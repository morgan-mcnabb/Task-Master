using Domain.Tasks;
using FluentValidation;
using TaskMasterApi.Contracts.Tasks;
using ValidationRules = Domain.Common.Validation;

namespace TaskMasterApi.Validation.Tasks;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title cannot be blank or whitespace.")
            .MaximumLength(ValidationRules.Task.TitleMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(ValidationRules.Task.DescriptionMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Priority)
            .IsInEnum();

        // Tags: each must be non-empty after trimming, and no duplicates (case-insensitive)
        RuleForEach(x => x.Tags)
            .Must(name => !string.IsNullOrWhiteSpace(name?.Trim()))
            .WithMessage("Tag names cannot be blank.");

        RuleFor(x => x.Tags)
            .Must(tags => tags.Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.ToUpperInvariant())
                .Distinct()
                .Count() == tags.Count)
            .WithMessage("Duplicate tags are not allowed.");
    }
}