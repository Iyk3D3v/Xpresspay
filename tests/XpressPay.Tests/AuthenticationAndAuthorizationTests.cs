using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using XpressPay.Application.DTOs;
using XpressPay.Domain.Constants;
using Xunit;

namespace XpressPay.Tests;

[Collection("IntegrationTests")]
public class AuthenticationAndAuthorizationTests
{
    private readonly HttpClient _client;

    public AuthenticationAndAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccessAndJwtToken()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var request = new RegisterRequestDto
        {
            Email = $"alice_{uniqueSuffix}@xpresspay.com",
            UserName = $"alice_{uniqueSuffix}",
            Password = "Password@123",
            FullName = "Alice Tester"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResult = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResult);
        Assert.True(authResult.Success);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.NotNull(authResult.User);
        Assert.Contains(Roles.User, authResult.User.Roles);
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsSuccessAndAdminRole()
    {
        var request = new LoginRequestDto
        {
            EmailOrUserName = "admin@xpresspay.com",
            Password = "Admin@123456"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResult = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResult);
        Assert.True(authResult.Success);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.Contains(Roles.Admin, authResult.User!.Roles);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401Unauthorized()
    {
        var request = new LoginRequestDto
        {
            EmailOrUserName = "admin@xpresspay.com",
            Password = "WrongPassword999!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedProfile_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/protected/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedProfile_WithValidToken_Returns200OK()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var registerDto = new RegisterRequestDto
        {
            Email = $"bob_{uniqueSuffix}@xpresspay.com",
            UserName = $"bob_{uniqueSuffix}",
            Password = "Password@123",
            FullName = "Bob Smith"
        };
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(regResult?.Token);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/protected/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", regResult.Token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_WithRegularUserToken_Returns403Forbidden()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var registerDto = new RegisterRequestDto
        {
            Email = $"charlie_{uniqueSuffix}@xpresspay.com",
            UserName = $"charlie_{uniqueSuffix}",
            Password = "Password@123"
        };
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(regResult?.Token);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", regResult.Token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_WithAdminToken_Returns200OK()
    {
        var loginDto = new LoginRequestDto
        {
            EmailOrUserName = "admin@xpresspay.com",
            Password = "Admin@123456"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginResult?.Token);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanAssignAdminRoleToUser_AndUserCanThenAccessAdminDashboard()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var registerDto = new RegisterRequestDto
        {
            Email = $"david_{uniqueSuffix}@xpresspay.com",
            UserName = $"david_{uniqueSuffix}",
            Password = "Password@123"
        };
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        var regularUserId = regResult!.User!.Id;

        var adminLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            EmailOrUserName = "admin@xpresspay.com",
            Password = "Admin@123456"
        });
        var adminResult = await adminLogin.Content.ReadFromJsonAsync<AuthResponseDto>();

        using var assignRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/assign-role");
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminResult!.Token);
        assignRequest.Content = JsonContent.Create(new AssignRoleRequestDto
        {
            UserId = regularUserId,
            Role = Roles.Admin
        });
        var assignResponse = await _client.SendAsync(assignRequest);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var reLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            EmailOrUserName = registerDto.UserName,
            Password = registerDto.Password
        });
        var reLoginResult = await reLoginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.Contains(Roles.Admin, reLoginResult!.User!.Roles);

        using var adminDashboardReq = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        adminDashboardReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", reLoginResult.Token);
        var dashboardResponse = await _client.SendAsync(adminDashboardReq);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
    }
}
