using System.ComponentModel.DataAnnotations;

namespace XpressPay.Application.DTOs;

public class LoginRequestDto
{
    [Required]
    public string EmailOrUserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
