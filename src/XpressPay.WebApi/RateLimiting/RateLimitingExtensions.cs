using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace XpressPay.WebApi.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                httpContext.Response.ContentType = "application/json";

                var retryAfterSeconds = 10;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterTimeSpan))
                {
                    retryAfterSeconds = Math.Max(1, (int)retryAfterTimeSpan.TotalSeconds);
                }

                httpContext.Response.Headers[RateLimitingConstants.RetryAfter] = retryAfterSeconds.ToString();
                httpContext.Response.Headers[RateLimitingConstants.RateLimitRemaining] = "0";
                httpContext.Response.Headers[RateLimitingConstants.RateLimitReset] = 
                    DateTimeOffset.UtcNow.AddSeconds(retryAfterSeconds).ToString("R");

                var errorResponse = new
                {
                    statusCode = StatusCodes.Status429TooManyRequests,
                    error = "Too Many Requests",
                    message = $"Rate limit exceeded. Please retry after {retryAfterSeconds} seconds.",
                    retryAfterSeconds = retryAfterSeconds,
                    timestamp = DateTime.UtcNow
                };

                await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
            };

            options.AddPolicy(RateLimitingConstants.IpPolicy, httpContext =>
            {
                var remoteIp = ResolveClientIp(httpContext);
                httpContext.Response.Headers[RateLimitingConstants.RateLimitLimit] = "5";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"ip:{remoteIp}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromSeconds(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(RateLimitingConstants.ApiKeyPolicy, httpContext =>
            {
                var apiKey = httpContext.Request.Headers[RateLimitingConstants.ApiKeyHeader].FirstOrDefault()
                             ?? httpContext.Request.Query["apiKey"].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    if (apiKey.Equals(RateLimitingConstants.PremiumApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Headers[RateLimitingConstants.RateLimitLimit] = "15";
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"key:premium:{apiKey}",
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 15,
                                Window = TimeSpan.FromSeconds(10),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    }

                    if (apiKey.Equals(RateLimitingConstants.StandardApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext.Response.Headers[RateLimitingConstants.RateLimitLimit] = "8";
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"key:standard:{apiKey}",
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 8,
                                Window = TimeSpan.FromSeconds(10),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    }
                }

                var remoteIp = ResolveClientIp(httpContext);
                httpContext.Response.Headers[RateLimitingConstants.RateLimitLimit] = "3";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"key:anonymous-ip:{remoteIp}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromSeconds(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(RateLimitingConstants.SlidingPolicy, httpContext =>
            {
                var remoteIp = ResolveClientIp(httpContext);
                httpContext.Response.Headers[RateLimitingConstants.RateLimitLimit] = "6";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"sliding:{remoteIp}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 6,
                        Window = TimeSpan.FromSeconds(10),
                        SegmentsPerWindow = 3,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    private static string ResolveClientIp(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ip = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(ip)) return ip;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}
