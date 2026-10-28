using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Hangfire;
using XpressPay.Application;
using XpressPay.Application.Features.Checkouts;
using XpressPay.Application.Features.Reports;
using XpressPay.Infrastructure;
using XpressPay.Infrastructure.BackgroundJobs;
using XpressPay.Infrastructure.Clients;
using XpressPay.Infrastructure.Persistence;
using XpressPay.WebApi.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCustomRateLimiting(builder.Configuration);
builder.Services.AddCustomHangfire(builder.Configuration);

builder.Services.AddHttpClient<IWalletServiceClient, WalletServiceClient>(client =>
{
    var walletUrl = builder.Configuration["Services:WalletService:Url"] ?? "http://localhost:5050";
    client.BaseAddress = new Uri(walletUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    var serviceKey = builder.Configuration["InternalServiceKey"] ?? "xpresspay-internal-mesh-secret-key-2026";
    client.DefaultRequestHeaders.Add("X-Internal-Service-Key", serviceKey);
});

var jwtSecret = builder.Configuration["JwtSettings:Secret"] 
                ?? "SuperSecretKeyForXpressPayDevelopmentCleanArchitecture12345!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "XpressPayAPI";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "XpressPayClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "XpressPay Clean Architecture Auth API",
        Version = "v1",
        Description = "ASP.NET Core Web API demonstrating Identity Framework authentication and role-based authorization."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token in the format: {token}"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    var schemeReference = new OpenApiSecuritySchemeReference("Bearer", null, null);
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            schemeReference,
            new List<string>()
        }
    });
});

var app = builder.Build();

await DatabaseSeeder.SeedDatabaseAsync(app.Services);
await OptimizationDataSeeder.SeedOptimizationDataAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "XpressPay API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
    DashboardTitle = "XpressPay Background Jobs Dashboard"
});

RecurringJob.AddOrUpdate<IFinancialReportJob>(
    "daily-financial-settlement-report",
    job => job.GenerateAndEmailDailySettlementReportAsync("admin@xpresspay.com", false, CancellationToken.None),
    Cron.Daily);

app.MapControllers();

app.Run();

namespace XpressPay.WebApi
{
    public partial class WebApiProgram { }
}
