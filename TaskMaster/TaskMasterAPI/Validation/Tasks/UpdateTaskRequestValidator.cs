using FluentValidation;
using TaskMasterApi.Contracts.Tasks;
using ValidationRules = Domain.Common.Validation;

namespace TaskMasterApi.Validation.Tasks;

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        When(x => x.Title is not null, () =>
        {
            RuleFor(x => x.Title!)
                .Must(title => !string.IsNullOrWhiteSpace(title))
                    .WithMessage("Title cannot be blank or whitespace.")
                .MaximumLength(ValidationRules.Task.TitleMaxLength);
        });

        When(x => x.Description is not null, () =>
        {
            RuleFor(x => x.Description!)
                .MaximumLength(ValidationRules.Task.DescriptionMaxLength);
        });

        When(x => x.Priority is not null, () =>
        {
            RuleFor(x => x.Priority!.Value)
                .IsInEnum();
        });

        When(x => x.Status is not null, () =>
        {
            RuleFor(x => x.Status!.Value)
                .IsInEnum();
        });

        // Tags: if provided (null means "leave tags unchanged")
        When(x => x.Tags is not null, () =>
        {
            RuleForEach(x => x.Tags!)
                .Must(name => !string.IsNullOrWhiteSpace(name?.Trim()))
                .WithMessage("Tag names cannot be blank.");

            RuleFor(x => x.Tags!)
                .Must(tags => tags.Select(t => t.Trim())
                                  .Where(t => !string.IsNullOrWhiteSpace(t))
                                  .Select(t => t.ToUpperInvariant())
                                  .Distinct()
                                  .Count() == tags.Count)
                .WithMessage("Duplicate tags are not allowed.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.ETag), () =>
        {
            RuleFor(x => x.ETag!)
                .Must(etag => {
                    try { _ = Convert.FromBase64String(etag); return true; }
                    catch { return false; }
                })
                .WithMessage("ETag must be a valid base64 string.");
        });
    }
}
