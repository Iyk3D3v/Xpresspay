using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using XpressPay.Application.DTOs;
using XpressPay.Domain.Enums;
using XpressPay.Infrastructure.Persistence;
using Xunit;

namespace XpressPay.Tests;

[Collection("IntegrationTests")]
public class OptimizedQueryTests
{
    private readonly HttpClient _client;

    public OptimizedQueryTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Scenario1_ProjectionAndNoTracking_ReturnsCustomerTransactions()
    {
        var response = await _client.GetAsync($"/api/optimized-queries/customer-summary/{OptimizationDataSeeder.KnownCustomerId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("count").GetInt32() > 0);

        var data = json.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0);

        var firstItem = data[0];
        Assert.False(string.IsNullOrWhiteSpace(firstItem.GetProperty("referenceNumber").GetString()));
        Assert.True(firstItem.GetProperty("amount").GetDecimal() > 0);
        Assert.Equal((int)TransactionStatus.Completed, firstItem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Scenario2_SplitQuery_ReturnsCustomerWithNestedTransactions()
    {
        var response = await _client.GetAsync($"/api/optimized-queries/customer-details-split/{OptimizationDataSeeder.KnownCustomerId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");

        Assert.Equal("Acme Global Logistics", data.GetProperty("name").GetString());
        var transactions = data.GetProperty("recentTransactions");
        Assert.True(transactions.GetArrayLength() > 0);
        Assert.True(transactions.GetArrayLength() <= 20);
    }

    [Fact]
    public async Task Scenario3_KeysetPagination_NavigatesPagesCorrectlyWithoutDuplicates()
    {
        var page1Resp = await _client.GetAsync("/api/optimized-queries/transactions-keyset?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, page1Resp.StatusCode);
        var page1Json = await page1Resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(page1Json.GetProperty("hasMore").GetBoolean());
        var page1Items = page1Json.GetProperty("data");
        Assert.Equal(10, page1Items.GetArrayLength());

        var cursor = page1Json.GetProperty("nextCursor");
        var lastCreatedAt = cursor.GetProperty("lastCreatedAt").GetString();
        var lastId = cursor.GetProperty("lastId").GetString();
        Assert.NotNull(lastCreatedAt);
        Assert.NotNull(lastId);

        var page2Resp = await _client.GetAsync($"/api/optimized-queries/transactions-keyset?lastCreatedAt={Uri.EscapeDataString(lastCreatedAt)}&lastId={lastId}&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, page2Resp.StatusCode);
        var page2Json = await page2Resp.Content.ReadFromJsonAsync<JsonElement>();
        var page2Items = page2Json.GetProperty("data");
        Assert.Equal(10, page2Items.GetArrayLength());

        var page1Ids = new HashSet<string>();
        for (int i = 0; i < page1Items.GetArrayLength(); i++)
        {
            page1Ids.Add(page1Items[i].GetProperty("id").GetString()!);
        }

        for (int i = 0; i < page2Items.GetArrayLength(); i++)
        {
            var page2Id = page2Items[i].GetProperty("id").GetString()!;
            Assert.DoesNotContain(page2Id, page1Ids);
        }
    }

    [Fact]
    public async Task Scenario4_ServerSideAggregates_ReturnsGroupedMetrics()
    {
        var response = await _client.GetAsync("/api/optimized-queries/daily-aggregates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("totalSummaryRows").GetInt32() > 0);

        var data = json.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0);

        var firstDay = data[0];
        Assert.True(firstDay.GetProperty("totalCount").GetInt32() > 0);
        Assert.True(firstDay.GetProperty("totalVolume").GetDecimal() > 0);
        Assert.True(firstDay.GetProperty("averageAmount").GetDecimal() > 0);
    }

    [Fact]
    public async Task Scenario5a_SARGableExistenceCheck_ReturnsTrueForActivePending()
    {
        var response = await _client.GetAsync($"/api/optimized-queries/check-active/{OptimizationDataSeeder.KnownCustomerId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("hasActivePendingTransactions").GetBoolean());
    }

    [Fact]
    public async Task Scenario5b_SARGableRangeQuery_ReturnsTransactionsWithinRange()
    {
        var from = DateTime.UtcNow.AddDays(-15).ToString("O");
        var to = DateTime.UtcNow.ToString("O");
        var response = await _client.GetAsync($"/api/optimized-queries/by-date-range?fromDate={Uri.EscapeDataString(from)}&toDate={Uri.EscapeDataString(to)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("count").GetInt32() > 0);
    }

    [Fact]
    public async Task Scenario6_DirectBulkUpdate_ExpiresStaleTransactionsInSingleRoundTrip()
    {
        var response = await _client.PostAsync("/api/optimized-queries/expire-stale?hoursAgo=48", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var updated = json.GetProperty("transactionsExpired").GetInt32();
        Assert.True(updated >= 15);
    }

    [Fact]
    public async Task Scenario7_PrecompiledQuery_RetrievesTransactionByReference()
    {
        var response = await _client.GetAsync($"/api/optimized-queries/by-reference/{OptimizationDataSeeder.KnownReferenceNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");

        Assert.Equal(OptimizationDataSeeder.KnownReferenceNumber, data.GetProperty("referenceNumber").GetString());
        Assert.Equal(1450.50m, data.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", data.GetProperty("currency").GetString());
    }
}
