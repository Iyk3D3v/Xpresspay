using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XpressPay.Application.Common.Interfaces;
using XpressPay.Application.DTOs;
using XpressPay.Domain.Constants;

namespace XpressPay.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;

    public AdminController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var users = await _authService.GetAllUsersAsync();

        return Ok(new
        {
            Message = "Welcome to the Admin Dashboard! You have administrator privileges.",
            TotalUsers = users.Count(),
            ServerTime = DateTime.UtcNow,
            Status = "Operational"
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpPost("assign-role")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _authService.AssignRoleAsync(request);
        if (!success)
        {
            return BadRequest(new { Message = $"Failed to assign role '{request.Role}' to user '{request.UserId}'." });
        }

        return Ok(new { Message = $"Role '{request.Role}' successfully assigned to user '{request.UserId}'." });
    }
}
