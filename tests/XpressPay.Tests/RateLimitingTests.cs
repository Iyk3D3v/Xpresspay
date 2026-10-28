using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using XpressPay.WebApi.RateLimiting;
using Xunit;

namespace XpressPay.Tests;

[Collection("IntegrationTests")]
public class RateLimitingTests
{
    private readonly HttpClient _client;

    public RateLimitingTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IpRateLimiting_AllowsUpToFiveRequests_AndRejectsSixthWith429AndHeaders()
    {
        var clientIp = $"198.51.100.{Random.Shared.Next(1, 250)}";

        for (int i = 1; i <= 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/ip-limited");
            request.Headers.Add("X-Forwarded-For", clientIp);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains(RateLimitingConstants.RateLimitLimit));
            Assert.Equal("5", response.Headers.GetValues(RateLimitingConstants.RateLimitLimit).FirstOrDefault());
        }

        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/ip-limited");
        rejectedRequest.Headers.Add("X-Forwarded-For", clientIp);

        var rejectedResponse = await _client.SendAsync(rejectedRequest);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.True(rejectedResponse.Headers.Contains(RateLimitingConstants.RetryAfter));
        Assert.True(rejectedResponse.Headers.Contains(RateLimitingConstants.RateLimitRemaining));
        Assert.Equal("0", rejectedResponse.Headers.GetValues(RateLimitingConstants.RateLimitRemaining).FirstOrDefault());
        Assert.True(rejectedResponse.Headers.Contains(RateLimitingConstants.RateLimitReset));

        var contentString = await rejectedResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(contentString);
        var root = jsonDoc.RootElement;
        Assert.Equal(429, root.GetProperty("statusCode").GetInt32());
        Assert.Equal("Too Many Requests", root.GetProperty("error").GetString());
        Assert.Contains("Rate limit exceeded", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task IpRateLimiting_PartitionsLimitsIndependentlyPerClientIp()
    {
        var ipAlpha = "203.0.113.10";
        var ipBeta = "203.0.113.20";

        for (int i = 1; i <= 5; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/ip-limited");
            req.Headers.Add("X-Forwarded-For", ipAlpha);
            var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        using var rejectedAlpha = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/ip-limited");
        rejectedAlpha.Headers.Add("X-Forwarded-For", ipAlpha);
        var resRejected = await _client.SendAsync(rejectedAlpha);
        Assert.Equal(HttpStatusCode.TooManyRequests, resRejected.StatusCode);

        using var reqBeta = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/ip-limited");
        reqBeta.Headers.Add("X-Forwarded-For", ipBeta);
        var resBeta = await _client.SendAsync(reqBeta);

        Assert.Equal(HttpStatusCode.OK, resBeta.StatusCode);
    }

    [Fact]
    public async Task ApiKeyRateLimiting_PremiumTier_AllowsUpToFifteenRequests_AndRejectsSixteenth()
    {
        for (int i = 1; i <= 15; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/api-key-limited");
            request.Headers.Add(RateLimitingConstants.ApiKeyHeader, RateLimitingConstants.PremiumApiKey);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/api-key-limited");
        rejectedRequest.Headers.Add(RateLimitingConstants.ApiKeyHeader, RateLimitingConstants.PremiumApiKey);

        var rejectedResponse = await _client.SendAsync(rejectedRequest);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.True(rejectedResponse.Headers.Contains(RateLimitingConstants.RetryAfter));
    }

    [Fact]
    public async Task ApiKeyRateLimiting_StandardTier_AllowsUpToEightRequests_AndRejectsNinth()
    {
        for (int i = 1; i <= 8; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/api-key-limited");
            request.Headers.Add(RateLimitingConstants.ApiKeyHeader, RateLimitingConstants.StandardApiKey);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/api-key-limited");
        rejectedRequest.Headers.Add(RateLimitingConstants.ApiKeyHeader, RateLimitingConstants.StandardApiKey);

        var rejectedResponse = await _client.SendAsync(rejectedRequest);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.True(rejectedResponse.Headers.Contains(RateLimitingConstants.RetryAfter));
    }

    [Fact]
    public async Task ApiKeyRateLimiting_WithoutKey_FallsBackToAnonymousIpTier_AllowsThreeRequests()
    {
        var anonIp = $"192.0.2.{Random.Shared.Next(1, 250)}";

        for (int i = 1; i <= 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/api-key-limited");
            request.Headers.Add("X-Forwarded-For", anonIp);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/api-key-limited");
        rejectedRequest.Headers.Add("X-Forwarded-For", anonIp);

        var rejectedResponse = await _client.SendAsync(rejectedRequest);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task UnlimitedEndpoint_WithDisableRateLimiting_AllowsHighVolumeWithoutRejection()
    {
        for (int i = 0; i < 15; i++)
        {
            var response = await _client.GetAsync("/api/rate-limiting/unlimited");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task RateLimitingStatus_ReturnsMetadataAndDetectedTier()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rate-limiting/status");
        request.Headers.Add(RateLimitingConstants.ApiKeyHeader, RateLimitingConstants.PremiumApiKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.Equal(RateLimitingConstants.PremiumApiKey, root.GetProperty("detectedApiKey").GetString());
        Assert.Contains("Premium", root.GetProperty("effectiveApiKeyTier").GetString());
        Assert.True(root.TryGetProperty("policies", out _));
    }
}
