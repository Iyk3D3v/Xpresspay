using System.ComponentModel.DataAnnotations;

namespace XpressPay.Application.DTOs;

public class AssignRoleRequestDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}
