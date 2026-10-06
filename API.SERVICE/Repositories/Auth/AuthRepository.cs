using API.DA.DbContexts;
using API.DA.Entities;
using API.SERVICE.Interfaces.Auth;
using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Auth;

public sealed class AuthRepository : IAuthRepository
{
    private readonly ElRenacerDbContext _context;

    public AuthRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<AspnetUsers?> GetMembershipUserAsync(string userName, CancellationToken cancellationToken = default)
    {
        var lowered = userName.ToLowerInvariant();
        return _context.AspnetUsers
            .Include(u => u.AspnetMembership)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.LoweredUserName == lowered, cancellationToken);
    }

    public Task<Usuarios?> GetUsuarioAsync(string userName, CancellationToken cancellationToken = default) =>
        _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Usuario == userName, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
