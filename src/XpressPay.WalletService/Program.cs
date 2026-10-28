using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using XpressPay.WalletService.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("WalletConnection") ?? "Data Source=wallet.db";
builder.Services.AddDbContext<WalletDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "XpressPay Wallet & Ledger Microservice",
        Version = "v1",
        Description = "Independent microservice managing customer wallet accounts, balance checks, and ledger transactions in Nigerian Naira (NGN)."
    });
});

var app = builder.Build();

await WalletDataSeeder.SeedWalletDataAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wallet API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();

namespace XpressPay.WalletService
{
    public partial class WalletServiceProgram { }
}
