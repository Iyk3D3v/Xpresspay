using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace XpressPay.WalletService.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ServiceAuthAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Service-Key";
    public const string DefaultKey = "xpresspay-internal-mesh-secret-key-2026";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedKey = configuration["InternalServiceKey"] ?? DefaultKey;

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var extractedKey) ||
            !string.Equals(extractedKey, expectedKey, StringComparison.Ordinal))
        {
            context.Result = new JsonResult(new
            {
                statusCode = StatusCodes.Status401Unauthorized,
                error = "UnauthorizedInterServiceCall",
                message = $"Access denied: Missing or invalid '{HeaderName}' header. Access restricted to authorized internal microservices.",
                timestamp = DateTime.UtcNow
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        await next();
    }
}
