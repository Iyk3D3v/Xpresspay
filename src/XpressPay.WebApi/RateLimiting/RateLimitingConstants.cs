namespace XpressPay.WebApi.RateLimiting;

public static class RateLimitingConstants
{
    public const string IpPolicy = "ip-policy";
    public const string ApiKeyPolicy = "api-key-policy";
    public const string SlidingPolicy = "sliding-policy";

    public const string ApiKeyHeader = "X-Api-Key";
    public const string RateLimitLimit = "X-RateLimit-Limit";
    public const string RateLimitRemaining = "X-RateLimit-Remaining";
    public const string RateLimitReset = "X-RateLimit-Reset";
    public const string RetryAfter = "Retry-After";

    public const string PremiumApiKey = "xpresspay-premium-live-key";
    public const string StandardApiKey = "xpresspay-standard-live-key";
}
