namespace API.SERVICE.Models.Auth;

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string Usuario,
    string? Nombre,
    string? Imagen,
    int? IdUsuario,
    int? IdSucursal,
    IReadOnlyList<string> Roles);
