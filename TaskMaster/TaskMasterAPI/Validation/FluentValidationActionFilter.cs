using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskMasterApi.Validation;

public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var serviceProvider = context.HttpContext.RequestServices;
        var cancellationToken = context.HttpContext.RequestAborted;

        var allFailures = new List<ValidationFailure>();

        foreach (var (argumentName, argumentValue) in context.ActionArguments)
        {
            if (argumentValue is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argumentValue.GetType());
            var validatorObj = serviceProvider.GetService(validatorType);
            if (validatorObj is not IValidator validator)
                continue;

            var validationContextType = typeof(ValidationContext<>).MakeGenericType(argumentValue.GetType());
            var validationContext = Activator.CreateInstance(validationContextType, argumentValue)
                                   as IValidationContext;

            if (validationContext is null)
                continue;

            var result = await validator.ValidateAsync(validationContext, cancellationToken);
            if (!result.IsValid)
                allFailures.AddRange(result.Errors);
        }

        if (allFailures.Count > 0)
        {
            var problem = new ValidationProblemDetails(
                allFailures
                    .GroupBy(f => f.PropertyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(f => f.ErrorMessage).Distinct().ToArray(),
                        StringComparer.OrdinalIgnoreCase))
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest
            };

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            return;
        }

        await next();
    }
}
