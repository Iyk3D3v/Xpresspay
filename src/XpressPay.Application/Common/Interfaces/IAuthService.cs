using XpressPay.Application.DTOs;

namespace XpressPay.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<bool> AssignRoleAsync(AssignRoleRequestDto request);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(string userId);
}
