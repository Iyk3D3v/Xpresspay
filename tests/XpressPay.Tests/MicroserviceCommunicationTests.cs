using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XpressPay.Application.Features.Checkouts;
using XpressPay.Infrastructure.Clients;
using XpressPay.WalletService.DTOs;
using XpressPay.WalletService.Persistence;
using XpressPay.WalletService.Security;
using Xunit;

namespace XpressPay.Tests;

public class WalletServiceTestFactory : WebApplicationFactory<XpressPay.WalletService.WalletServiceProgram>
{
    private readonly string _dbName = $"wallet_test_{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WalletDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<WalletDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_dbName}");
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbName)) File.Delete(_dbName);
            if (File.Exists($"{_dbName}-shm")) File.Delete($"{_dbName}-shm");
            if (File.Exists($"{_dbName}-wal")) File.Delete($"{_dbName}-wal");
        }
        catch { }
    }
}

[Collection("IntegrationTests")]
public class MicroserviceCommunicationTests : IClassFixture<WalletServiceTestFactory>
{
    private readonly WalletServiceTestFactory _walletFactory;
    private readonly CustomWebApplicationFactory _paymentFactory;

    public MicroserviceCommunicationTests(
        WalletServiceTestFactory walletFactory,
        CustomWebApplicationFactory paymentFactory)
    {
        _walletFactory = walletFactory;
        _paymentFactory = paymentFactory;
    }

    [Fact]
    public async Task WalletService_DirectCall_WithoutValidServiceKey_Returns401Unauthorized()
    {
        var client = _walletFactory.CreateClient();
        var debitRequest = new DebitWalletRequest
        {
            AccountId = WalletDataSeeder.AliceWalletId,
            Amount = 5000.00m,
            Currency = "NGN",
            ReferenceNumber = "TEST-REF-001"
        };

        var responseMissingKey = await client.PostAsJsonAsync("/api/wallets/debit", debitRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, responseMissingKey.StatusCode);

        using var requestInvalidKey = new HttpRequestMessage(HttpMethod.Post, "/api/wallets/debit")
        {
            Content = JsonContent.Create(debitRequest)
        };
        requestInvalidKey.Headers.Add(ServiceAuthAttribute.HeaderName, "wrong-unauthorized-key");

        var responseInvalidKey = await client.SendAsync(requestInvalidKey);
        Assert.Equal(HttpStatusCode.Unauthorized, responseInvalidKey.StatusCode);
    }

    [Fact]
    public async Task WalletService_DirectCall_WithInvalidAmount_Returns400BadRequest()
    {
        var client = _walletFactory.CreateClient();
        client.DefaultRequestHeaders.Add(ServiceAuthAttribute.HeaderName, ServiceAuthAttribute.DefaultKey);

        var invalidRequest = new DebitWalletRequest
        {
            AccountId = WalletDataSeeder.AliceWalletId,
            Amount = -100.00m,
            Currency = "NGN",
            ReferenceNumber = "TEST-REF-002"
        };

        var response = await client.PostAsJsonAsync("/api/wallets/debit", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WalletService_DirectCall_WithUnsupportedCurrency_Returns400BadRequest()
    {
        var client = _walletFactory.CreateClient();
        client.DefaultRequestHeaders.Add(ServiceAuthAttribute.HeaderName, ServiceAuthAttribute.DefaultKey);

        var invalidRequest = new DebitWalletRequest
        {
            AccountId = WalletDataSeeder.AliceWalletId,
            Amount = 1000.00m,
            Currency = "USD",
            ReferenceNumber = "TEST-REF-003"
        };

        var response = await client.PostAsJsonAsync("/api/wallets/debit", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WalletService_DirectCall_WithNonExistentAccount_Returns404NotFound()
    {
        var client = _walletFactory.CreateClient();
        client.DefaultRequestHeaders.Add(ServiceAuthAttribute.HeaderName, ServiceAuthAttribute.DefaultKey);

        var nonExistentRequest = new DebitWalletRequest
        {
            AccountId = Guid.NewGuid(),
            Amount = 5000.00m,
            Currency = "NGN",
            ReferenceNumber = "TEST-REF-004"
        };

        var response = await client.PostAsJsonAsync("/api/wallets/debit", nonExistentRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WalletService_DirectCall_WithInsufficientFunds_Returns422UnprocessableEntity()
    {
        var client = _walletFactory.CreateClient();
        client.DefaultRequestHeaders.Add(ServiceAuthAttribute.HeaderName, ServiceAuthAttribute.DefaultKey);

        var insufficientRequest = new DebitWalletRequest
        {
            AccountId = WalletDataSeeder.BobWalletId,
            Amount = 50000.00m,
            Currency = "NGN",
            ReferenceNumber = "TEST-REF-005"
        };

        var response = await client.PostAsJsonAsync("/api/wallets/debit", insufficientRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InsufficientFundsErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("InsufficientFunds", body.Error);
        Assert.Equal(15000.00m, body.CurrentBalance);
        Assert.Equal(50000.00m, body.RequestedAmount);
        Assert.Equal(35000.00m, body.Shortfall);
        Assert.Equal("NGN", body.Currency);
    }

    [Fact]
    public async Task EndToEndCheckout_WithSufficientBalance_DebitsWalletAndCompletesOrder()
    {
        var walletHttpClient = _walletFactory.CreateClient();
        walletHttpClient.DefaultRequestHeaders.Add(ServiceAuthAttribute.HeaderName, ServiceAuthAttribute.DefaultKey);

        var paymentClient = _paymentFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWalletServiceClient));
                if (descriptor != null) services.Remove(descriptor);

                services.AddScoped<IWalletServiceClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<WalletServiceClient>>();
                    return new WalletServiceClient(walletHttpClient, logger);
                });
            });
        }).CreateClient();

        var checkoutRequest = new CheckoutRequestDto
        {
            CustomerAccountId = WalletDataSeeder.AliceWalletId,
            Amount = 50000.00m,
            Currency = "NGN",
            ItemDescription = "MacBook Air M3 Sleeve"
        };

        var response = await paymentClient.PostAsJsonAsync("/api/checkouts", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkoutResult = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        Assert.NotNull(checkoutResult);
        Assert.Equal("Completed", checkoutResult.Status);
        Assert.Equal(50000.00m, checkoutResult.AmountPaid);
        Assert.Equal("NGN", checkoutResult.Currency);
        Assert.Equal(450000.00m, checkoutResult.RemainingWalletBalance);
        Assert.False(string.IsNullOrWhiteSpace(checkoutResult.ReferenceNumber));
        Assert.NotEqual(Guid.Empty, checkoutResult.DebitTransactionId);
    }

    [Fact]
    public async Task EndToEndCheckout_WithInsufficientBalance_Returns422UnprocessableEntity()
    {
        var walletHttpClient = _walletFactory.CreateClient();
        walletHttpClient.DefaultRequestHeaders.Add(ServiceAuthAttribute.HeaderName, ServiceAuthAttribute.DefaultKey);

        var paymentClient = _paymentFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWalletServiceClient));
                if (descriptor != null) services.Remove(descriptor);

                services.AddScoped<IWalletServiceClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<WalletServiceClient>>();
                    return new WalletServiceClient(walletHttpClient, logger);
                });
            });
        }).CreateClient();

        var checkoutRequest = new CheckoutRequestDto
        {
            CustomerAccountId = WalletDataSeeder.BobWalletId,
            Amount = 50000.00m,
            Currency = "NGN",
            ItemDescription = "Gaming Monitor 27-inch"
        };

        var response = await paymentClient.PostAsJsonAsync("/api/checkouts", checkoutRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(422, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("InsufficientWalletBalance", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EndToEndCheckout_WhenWalletServiceUnavailable_Returns503ServiceUnavailable()
    {
        var unreachableClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:59999") };
        unreachableClient.Timeout = TimeSpan.FromMilliseconds(500);

        var paymentClient = _paymentFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWalletServiceClient));
                if (descriptor != null) services.Remove(descriptor);

                services.AddScoped<IWalletServiceClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<WalletServiceClient>>();
                    return new WalletServiceClient(unreachableClient, logger);
                });
            });
        }).CreateClient();

        var checkoutRequest = new CheckoutRequestDto
        {
            CustomerAccountId = WalletDataSeeder.AliceWalletId,
            Amount = 10000.00m,
            Currency = "NGN",
            ItemDescription = "Wireless Earbuds"
        };

        var response = await paymentClient.PostAsJsonAsync("/api/checkouts", checkoutRequest);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(503, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("ServiceUnavailable", doc.RootElement.GetProperty("error").GetString());
    }
}
