using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XpressPay.Domain.Constants;

namespace XpressPay.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProtectedController : ControllerBase
{
    [Authorize]
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new
        {
            Message = "Access granted to authenticated profile.",
            UserId = userId,
            UserName = userName,
            Email = email,
            Roles = roles
        });
    }

    [Authorize(Roles = Roles.User)]
    [HttpGet("user-resource")]
    public IActionResult GetUserResource()
    {
        return Ok(new
        {
            Message = "Access granted: You have the required 'User' role to access this resource.",
            ServerTime = DateTime.UtcNow
        });
    }
}
