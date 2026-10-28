using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using XpressPay.Application.Common.Interfaces;
using XpressPay.Application.DTOs;
using XpressPay.Domain.Constants;
using XpressPay.Infrastructure.Identity;

namespace XpressPay.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingByEmail != null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "A user with this email already exists."
            };
        }

        var existingByUserName = await _userManager.FindByNameAsync(request.UserName);
        if (existingByUserName != null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "A user with this username already exists."
            };
        }

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.UserName,
            FullName = request.FullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Failed to register user.",
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        if (!await _roleManager.RoleExistsAsync(Roles.User))
        {
            await _roleManager.CreateAsync(new IdentityRole(Roles.User));
        }
        await _userManager.AddToRoleAsync(user, Roles.User);

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.UserName!, user.Email!, roles);

        return new AuthResponseDto
        {
            Success = true,
            Message = "User registered successfully.",
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                UserName = user.UserName!,
                FullName = user.FullName,
                Roles = roles
            }
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.EmailOrUserName)
                   ?? await _userManager.FindByNameAsync(request.EmailOrUserName);

        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Invalid credentials."
            };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.UserName!, user.Email!, roles);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Login successful.",
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                UserName = user.UserName!,
                FullName = user.FullName,
                Roles = roles
            }
        };
    }

    public async Task<bool> AssignRoleAsync(AssignRoleRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return false;
        }

        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole(request.Role));
            if (!roleResult.Succeeded)
            {
                return false;
            }
        }

        if (await _userManager.IsInRoleAsync(user, request.Role))
        {
            return true;
        }

        var result = await _userManager.AddToRoleAsync(user, request.Role);
        return result.Succeeded;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Roles = roles
            });
        }

        return userDtos;
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Roles = roles
        };
    }
}
