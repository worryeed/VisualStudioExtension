using CodeAI.Api.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace CodeAI.Api.Attribute;

[AttributeUsage(AttributeTargets.Method)]
public class ValidateRequestAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.ActionArguments.Values.OfType<CodeRequest>().FirstOrDefault();

        if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            context.Result = new BadRequestObjectResult("Prompt is required");
        }
    }
}