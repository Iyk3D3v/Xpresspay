using System.ComponentModel.DataAnnotations;

namespace XpressPay.Application.DTOs;

public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }
}
