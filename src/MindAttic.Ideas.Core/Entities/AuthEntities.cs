namespace MindAttic.Ideas.Core.Entities;

/// <summary>
/// Admin account. Ported from MindAttic.Frontpage: string Id, BCrypt PasswordHash, SecurityStamp
/// revalidated on every request, MustChangePassword bootstrap, soft-disable via IsActive.
/// </summary>
public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = UserRoles.Admin;
    /// <summary>Rotated on password/role change; mismatch on validate forces logout.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Role constants. Admin holds the <c>Cms.AuthorRawMarkup</c> claim (trusted inline JS/HTML).</summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public static readonly IReadOnlyList<string> All = new[] { Admin };
}

/// <summary>Claim types the CMS issues/checks.</summary>
public static class CmsClaims
{
    /// <summary>Holder may author raw, unsanitized HTML/JS in a page (Author trust). Granted to Admin.</summary>
    public const string AuthorRawMarkup = "Cms.AuthorRawMarkup";
}
