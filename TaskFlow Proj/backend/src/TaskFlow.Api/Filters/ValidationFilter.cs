using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskFlow.Api.Filters;

/// <summary>Runs the FluentValidation validator registered for each bound action argument before the action runs. A failure
/// short-circuits with a 400 validation problem whose <c>errors</c> map names each invalid field, so no endpoint has to call
/// a validator itself.</summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public ValidationFilter(ProblemDetailsFactory problemDetailsFactory)
    {
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var errors = new ModelStateDictionary();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null
                || services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                errors.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }
        }

        if (errors.ErrorCount > 0)
        {
            var problem = _problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, errors, StatusCodes.Status400BadRequest);
            context.Result = new BadRequestObjectResult(problem);
            return;
        }

        await next();
    }
}
