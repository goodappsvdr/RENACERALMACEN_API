namespace API.Security;

/// <summary>
/// La acción no está sujeta a permisos por área: la puede usar cualquier usuario logueado (p. ej. cambiar la propia contraseña).
/// Usar solo para operaciones sobre el propio usuario.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LibreDeAreaAttribute : Attribute;
