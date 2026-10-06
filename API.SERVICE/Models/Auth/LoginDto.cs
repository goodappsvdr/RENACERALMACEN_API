using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Auth;

public sealed class LoginDto
{
    [Required, MaxLength(256)]
    public string Usuario { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
