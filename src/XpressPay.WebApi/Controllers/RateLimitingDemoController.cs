using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using XpressPay.WebApi.RateLimiting;

namespace XpressPay.WebApi.Controllers;

[ApiController]
[Route("api/rate-limiting")]
public class RateLimitingDemoController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetRateLimitingStatus()
    {
        var apiKey = Request.Headers[RateLimitingConstants.ApiKeyHeader].FirstOrDefault();
        var clientIp = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                       ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "127.0.0.1";

        string tier = "Anonymous / Untrusted (3 requests / 10s)";
        if (!string.IsNullOrEmpty(apiKey))
        {
            if (apiKey.Equals(RateLimitingConstants.PremiumApiKey, StringComparison.OrdinalIgnoreCase))
                tier = "Premium API Key (15 requests / 10s)";
            else if (apiKey.Equals(RateLimitingConstants.StandardApiKey, StringComparison.OrdinalIgnoreCase))
                tier = "Standard API Key (8 requests / 10s)";
        }

        return Ok(new
        {
            Message = "ASP.NET Core Native Rate Limiting Status",
            ClientIp = clientIp,
            DetectedApiKey = apiKey ?? "(None provided)",
            EffectiveApiKeyTier = tier,
            Policies = new
            {
                IpPolicy = "5 requests per 10-second fixed window per IP",
                ApiKeyPolicy = "Tiered by X-Api-Key: Premium (15 req/10s), Standard (8 req/10s), Anonymous (3 req/10s)",
                SlidingPolicy = "6 requests per 10-second sliding window (3 segments)",
                Unlimited = "Bypassed via [DisableRateLimiting]"
            },
            DemoKeys = new
            {
                Premium = RateLimitingConstants.PremiumApiKey,
                Standard = RateLimitingConstants.StandardApiKey
            }
        });
    }

    [HttpGet("ip-limited")]
    [EnableRateLimiting(RateLimitingConstants.IpPolicy)]
    public IActionResult GetIpLimitedResource()
    {
        return Ok(new
        {
            Message = "Success: Request granted under IP Rate Limiting Policy (5 requests / 10 seconds).",
            ServerTime = DateTime.UtcNow
        });
    }

    [HttpGet("api-key-limited")]
    [EnableRateLimiting(RateLimitingConstants.ApiKeyPolicy)]
    public IActionResult GetApiKeyLimitedResource()
    {
        var apiKey = Request.Headers[RateLimitingConstants.ApiKeyHeader].FirstOrDefault()
                     ?? Request.Query["apiKey"].FirstOrDefault();

        return Ok(new
        {
            Message = "Success: Request granted under Tiered API Key Rate Limiting Policy.",
            ApiKeyUsed = apiKey ?? "(Anonymous IP fallback)",
            ServerTime = DateTime.UtcNow
        });
    }

    [HttpGet("sliding-limited")]
    [EnableRateLimiting(RateLimitingConstants.SlidingPolicy)]
    public IActionResult GetSlidingLimitedResource()
    {
        return Ok(new
        {
            Message = "Success: Request granted under Sliding Window Rate Limiting Policy (6 req / 10s).",
            ServerTime = DateTime.UtcNow
        });
    }

    [HttpGet("unlimited")]
    [DisableRateLimiting]
    public IActionResult GetUnlimitedResource()
    {
        return Ok(new
        {
            Message = "Success: Rate limiting is disabled on this endpoint.",
            ServerTime = DateTime.UtcNow
        });
    }
}
