using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Services;

/// <summary>
/// Authentication essentials ported from MindAttic.Frontpage: BCrypt hashing (work factor 12),
/// SecurityStamp rotation, idempotent admin bootstrap. Lockout/rate-limiting is added in the Web host.
/// </summary>
public sealed class AuthService(IDbContextFactory<CmsDbContext> dbFactory)
{
    private const int WorkFactor = 12;

    /// <summary>Verify a credential. Returns the user on success, else null.</summary>
    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);
        if (user is null) return null;
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    /// <summary>Idempotent admin bootstrap: create if missing; rotate password+stamp only if changed.</summary>
    public async Task SeedUserAsync(string username, string displayName, string password, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
        {
            db.Users.Add(new User
            {
                Username = username,
                DisplayName = displayName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, WorkFactor),
                Role = UserRoles.Admin,
                CreatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            return;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(ct);
        }
    }
}
