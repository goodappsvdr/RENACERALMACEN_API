using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Sistema;
using API.SERVICE.Security;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Sistema;

/// <summary>Reglas comunes de la administración de usuarios (FrmUsuariosABM).</summary>
internal static class UsuarioAdminContexto
{
    public const string RolAdministrador = "ADMINISTRADOR";
    public const int EstadoActivo = 15;
    public const int EstadoBaja = 16;

    /// <summary>Fecha "nula" de SqlMembershipProvider para bloqueos e intentos fallidos.</summary>
    public static readonly DateTime FechaNulaMembership = new(1754, 1, 1);

    /// <summary>El ERP no controlaba quién administraba usuarios: cualquier usuario logueado podía crear administradores.</summary>
    public static void RequireAdministrador(ICurrentUser user)
    {
        if (!user.IsInRole(RolAdministrador))
            throw new ForbiddenException("Solo un ADMINISTRADOR puede administrar usuarios.");
    }

    /// <summary>SqlMembershipProvider guarda las fechas en UTC y redondeadas al segundo.</summary>
    public static DateTime AhoraUtc(TimeProvider time)
    {
        var ahora = time.GetUtcNow().UtcDateTime;
        return new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    public static async Task<Db.AspnetRoles> ResolverRolAsync(IUsuarioAdminRepository usuarios, Guid idRol, CancellationToken ct) =>
        await usuarios.GetRolAsync(idRol, ct) ?? throw new BusinessException($"El rol {idRol} no existe.");

    public static async Task<List<int>> ValidarSucursalesAsync(IUsuarioAdminRepository usuarios, IEnumerable<int> sucursales, CancellationToken ct)
    {
        var pedidas = sucursales.Distinct().ToList();
        if (pedidas.Count == 0)
            throw new BusinessException("Seleccione al menos una sucursal.");
        var activas = (await usuarios.GetSucursalesActivasAsync(ct)).Select(s => s.IdSucursal).ToHashSet();
        var invalidas = pedidas.Where(s => !activas.Contains(s)).ToList();
        if (invalidas.Count > 0)
            throw new BusinessException($"Las sucursales {string.Join(", ", invalidas)} no existen o no están activas.");
        return pedidas;
    }

    public static void SetPassword(Db.AspnetMembership membresia, string password, DateTime ahoraUtc)
    {
        var (hash, salt) = MembershipPasswordHasher.Create(password);
        membresia.Password = hash;
        membresia.PasswordSalt = salt;
        membresia.PasswordFormat = MembershipPasswordHasher.HashedFormat;
        membresia.LastPasswordChangedDate = ahoraUtc;
    }
}

public interface IGetUsuariosAdminUseCase
{
    Task<IReadOnlyList<UsuarioAdminListaDisplay>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Usuarios del ERP con rol y estado (Usuarios_BuscarTodos), sin contraseñas.</summary>
public sealed class GetUsuariosAdminUseCase : IGetUsuariosAdminUseCase
{
    private readonly IUsuarioAdminRepository _usuarios;
    private readonly ICurrentUser _currentUser;

    public GetUsuariosAdminUseCase(IUsuarioAdminRepository usuarios, ICurrentUser currentUser)
    {
        _usuarios = usuarios;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<UsuarioAdminListaDisplay>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        UsuarioAdminContexto.RequireAdministrador(_currentUser);
        return (await _usuarios.GetUsuariosAsync(cancellationToken))
            .Select(u => new UsuarioAdminListaDisplay(u.IdUsuario, u.Nombre, u.Usuario, u.Email, u.IdEstado == UsuarioAdminContexto.EstadoActivo, u.Rol))
            .ToList();
    }
}

public interface IGetUsuarioAdminUseCase
{
    Task<UsuarioAdminDisplay> ExecuteAsync(int idUsuario, CancellationToken cancellationToken = default);
}

/// <summary>Detalle para editar (BuscarUsuario_PorID_Usuario_Ws), sin la contraseña.</summary>
public sealed class GetUsuarioAdminUseCase : IGetUsuarioAdminUseCase
{
    private readonly IUsuarioAdminRepository _usuarios;
    private readonly ICurrentUser _currentUser;

    public GetUsuarioAdminUseCase(IUsuarioAdminRepository usuarios, ICurrentUser currentUser)
    {
        _usuarios = usuarios;
        _currentUser = currentUser;
    }

    public async Task<UsuarioAdminDisplay> ExecuteAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        UsuarioAdminContexto.RequireAdministrador(_currentUser);
        var usuario = await _usuarios.GetUsuarioAsync(idUsuario, cancellationToken)
            ?? throw new NotFoundException($"Usuario {idUsuario} no existe.");
        var membership = usuario.UserId is { } userId ? await _usuarios.GetMembershipAsync(userId, cancellationToken) : null;
        var rol = membership?.Role.FirstOrDefault();
        var sucursales = await _usuarios.GetSucursalesDeUsuarioAsync(idUsuario, cancellationToken);

        return new UsuarioAdminDisplay(
            usuario.IdUsuario, usuario.Nombre, usuario.Usuario, usuario.Email, usuario.IdEstado == UsuarioAdminContexto.EstadoActivo,
            membership?.AspnetMembership?.IsLockedOut ?? false, rol?.RoleId, rol?.RoleName, usuario.IdSucursal, sucursales);
    }
}

public interface IGetUsuarioAdminOpcionesUseCase
{
    Task<UsuarioAdminOpcionesDisplay> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Combos de la pantalla: roles (CargarCboRoles_WS) y sucursales activas (CargarCboSucursales_WS).</summary>
public sealed class GetUsuarioAdminOpcionesUseCase : IGetUsuarioAdminOpcionesUseCase
{
    private readonly IUsuarioAdminRepository _usuarios;
    private readonly ICurrentUser _currentUser;

    public GetUsuarioAdminOpcionesUseCase(IUsuarioAdminRepository usuarios, ICurrentUser currentUser)
    {
        _usuarios = usuarios;
        _currentUser = currentUser;
    }

    public async Task<UsuarioAdminOpcionesDisplay> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        UsuarioAdminContexto.RequireAdministrador(_currentUser);
        var roles = (await _usuarios.GetRolesAsync(cancellationToken)).Select(r => new RolDisplay(r.RoleId, r.RoleName)).ToList();
        var sucursales = (await _usuarios.GetSucursalesActivasAsync(cancellationToken))
            .Select(s => new SucursalOpcionDisplay(s.IdSucursal, s.Descripcion, s.PuntoVenta)).ToList();
        return new UsuarioAdminOpcionesDisplay(roles, sucursales);
    }
}

public interface ICreateUsuarioAdminUseCase
{
    Task<UsuarioAdminDisplay> ExecuteAsync(CreateUsuarioAdminDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de usuario en una transacción (Usuarios_Agregar_Ws): usuario de Membership con el mismo formato de contraseña que
/// SqlMembershipProvider (así entra también al WebForms), rol, fila de Usuarios y sucursales.
/// </summary>
public sealed class CreateUsuarioAdminUseCase : ICreateUsuarioAdminUseCase
{
    private readonly IUsuarioAdminRepository _usuarios;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ICurrentUser _currentUser;

    public CreateUsuarioAdminUseCase(
        IUsuarioAdminRepository usuarios, IReferenciasRepository referencias, IUnitOfWork unitOfWork, TimeProvider time, ICurrentUser currentUser)
    {
        _usuarios = usuarios;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _time = time;
        _currentUser = currentUser;
    }

    public async Task<UsuarioAdminDisplay> ExecuteAsync(CreateUsuarioAdminDto dto, CancellationToken cancellationToken = default)
    {
        UsuarioAdminContexto.RequireAdministrador(_currentUser);
        var userName = dto.Usuario.Trim();

        var creado = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await _usuarios.ExisteUserNameAsync(userName, ct))
                throw new ConflictException("Nombre de usuario en uso.");

            var rol = await UsuarioAdminContexto.ResolverRolAsync(_usuarios, dto.IdRol!.Value, ct);
            var sucursales = await UsuarioAdminContexto.ValidarSucursalesAsync(_usuarios, dto.Sucursales, ct);
            var ahora = UsuarioAdminContexto.AhoraUtc(_time);
            var nula = UsuarioAdminContexto.FechaNulaMembership;

            var membership = new Db.AspnetUsers
            {
                ApplicationId = rol.ApplicationId, // la aplicación "/" del ERP (una sola en la base)
                UserId = Guid.NewGuid(),
                UserName = userName,
                LoweredUserName = userName.ToLowerInvariant(),
                IsAnonymous = false,
                LastActivityDate = ahora,
                AspnetMembership = new Db.AspnetMembership
                {
                    ApplicationId = rol.ApplicationId,
                    Email = dto.Email,
                    LoweredEmail = dto.Email.ToLowerInvariant(),
                    IsApproved = dto.Activo,
                    IsLockedOut = false,
                    CreateDate = ahora,
                    LastLoginDate = ahora,
                    LastLockoutDate = nula,
                    FailedPasswordAttemptWindowStart = nula,
                    FailedPasswordAnswerAttemptWindowStart = nula,
                },
            };
            UsuarioAdminContexto.SetPassword(membership.AspnetMembership, dto.Password, ahora);
            membership.Role.Add(rol);
            _usuarios.Add(membership);

            var usuario = new Db.Usuarios
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                Usuario = userName,
                Pass = string.Empty, // el ERP guardaba la contraseña en claro
                UserId = membership.UserId,
                Token = string.Empty,
                Imagen = await _referencias.GetParametroAsync("USER", "EMPRESA", ct),
                IdSucursal = sucursales[0],
                IdEstado = dto.Activo ? UsuarioAdminContexto.EstadoActivo : UsuarioAdminContexto.EstadoBaja,
            };
            _usuarios.Add(usuario);
            await _usuarios.SaveChangesAsync(ct);

            await _usuarios.ReemplazarSucursalesAsync(usuario.IdUsuario, sucursales, ct);
            await _usuarios.SaveChangesAsync(ct);

            return new UsuarioAdminDisplay(
                usuario.IdUsuario, usuario.Nombre, usuario.Usuario, usuario.Email, dto.Activo, false, rol.RoleId, rol.RoleName, usuario.IdSucursal, sucursales);
        }, cancellationToken);

        return creado;
    }
}

public interface IUpdateUsuarioAdminUseCase
{
    Task<UsuarioAdminDisplay> ExecuteAsync(int idUsuario, UpdateUsuarioAdminDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modificación (Usuarios_Modificar_Ws): datos, estado (Usuarios.ID_Estado y aspnet_Membership.IsApproved), email, rol,
/// sucursales y, si se informa, contraseña. El nombre de usuario no cambia (el ERP lo cambiaba solo en Usuarios y rompía el login).
/// </summary>
public sealed class UpdateUsuarioAdminUseCase : IUpdateUsuarioAdminUseCase
{
    private readonly IUsuarioAdminRepository _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ICurrentUser _currentUser;

    public UpdateUsuarioAdminUseCase(IUsuarioAdminRepository usuarios, IUnitOfWork unitOfWork, TimeProvider time, ICurrentUser currentUser)
    {
        _usuarios = usuarios;
        _unitOfWork = unitOfWork;
        _time = time;
        _currentUser = currentUser;
    }

    public async Task<UsuarioAdminDisplay> ExecuteAsync(int idUsuario, UpdateUsuarioAdminDto dto, CancellationToken cancellationToken = default)
    {
        UsuarioAdminContexto.RequireAdministrador(_currentUser);
        if (idUsuario == _currentUser.IdUsuario && !dto.Activo)
            throw new BusinessException("No puede darse de baja a sí mismo.");

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var usuario = await _usuarios.GetUsuarioAsync(idUsuario, ct)
                ?? throw new NotFoundException($"Usuario {idUsuario} no existe.");
            var membership = (usuario.UserId is { } userId ? await _usuarios.GetMembershipAsync(userId, ct) : null)
                ?? throw new ConflictException($"El usuario {idUsuario} no tiene usuario de Membership asociado.");
            var membresia = membership.AspnetMembership
                ?? throw new ConflictException($"El usuario {idUsuario} no tiene membresía en aspnet_Membership.");

            var rol = await UsuarioAdminContexto.ResolverRolAsync(_usuarios, dto.IdRol!.Value, ct);
            var sucursales = await UsuarioAdminContexto.ValidarSucursalesAsync(_usuarios, dto.Sucursales, ct);
            var ahora = UsuarioAdminContexto.AhoraUtc(_time);

            usuario.Nombre = dto.Nombre;
            usuario.Email = dto.Email;
            usuario.IdEstado = dto.Activo ? UsuarioAdminContexto.EstadoActivo : UsuarioAdminContexto.EstadoBaja;
            if (!sucursales.Contains(usuario.IdSucursal ?? 0))
                usuario.IdSucursal = sucursales[0];

            membresia.Email = dto.Email;
            membresia.LoweredEmail = dto.Email.ToLowerInvariant();
            membresia.IsApproved = dto.Activo;

            if (!string.IsNullOrEmpty(dto.Password))
            {
                UsuarioAdminContexto.SetPassword(membresia, dto.Password, ahora);
                usuario.Pass = string.Empty; // se borra la copia en claro que guardaba el ERP
            }

            // aspnet_userInRoles_Modificar: un solo rol por usuario.
            membership.Role.Clear();
            membership.Role.Add(rol);

            await _usuarios.ReemplazarSucursalesAsync(idUsuario, sucursales, ct);
            await _usuarios.SaveChangesAsync(ct);

            return new UsuarioAdminDisplay(
                usuario.IdUsuario, usuario.Nombre, usuario.Usuario, usuario.Email, dto.Activo, membresia.IsLockedOut, rol.RoleId, rol.RoleName,
                usuario.IdSucursal, sucursales);
        }, cancellationToken);
    }
}

public interface ICambiarPasswordUseCase
{
    Task ExecuteAsync(CambiarPasswordDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cambio de la contraseña propia (CambiarPass_Ws de FrmCambiarPass). Valida la actual contra el hash de Membership
/// (el ERP la comparaba con la copia en claro de Usuarios.Pass).
/// </summary>
public sealed class CambiarPasswordUseCase : ICambiarPasswordUseCase
{
    private readonly IUsuarioAdminRepository _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ICurrentUser _currentUser;

    public CambiarPasswordUseCase(IUsuarioAdminRepository usuarios, IUnitOfWork unitOfWork, TimeProvider time, ICurrentUser currentUser)
    {
        _usuarios = usuarios;
        _unitOfWork = unitOfWork;
        _time = time;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(CambiarPasswordDto dto, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException("Token sin usuario.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var membership = await _usuarios.GetMembershipAsync(userId, ct)
                ?? throw new UnauthorizedException("El usuario del token no existe.");
            var membresia = membership.AspnetMembership
                ?? throw new UnauthorizedException("El usuario del token no tiene membresía.");

            if (!MembershipPasswordHasher.Verify(dto.PasswordActual, membresia.Password, membresia.PasswordSalt, membresia.PasswordFormat))
                throw new BusinessException("La contraseña actual no es correcta.");

            UsuarioAdminContexto.SetPassword(membresia, dto.PasswordNueva, UsuarioAdminContexto.AhoraUtc(_time));

            if (_currentUser.IdUsuario is { } idUsuario && await _usuarios.GetUsuarioAsync(idUsuario, ct) is { } usuario)
                usuario.Pass = string.Empty;

            await _usuarios.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }
}
