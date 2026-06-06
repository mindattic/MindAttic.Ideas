using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Services;

public sealed record PageSummary(
    int Id, string Slug, string Title, PageKind Kind, bool IsPublished, bool Enabled, ContentTrust BodyTrust,
    int? ParentId, int SortOrder);

public sealed class PageEditModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ThemeKey { get; set; }
    public int? ThemeVersion { get; set; }
    public PageKind Kind { get; set; } = PageKind.Data;
    public string? BodyHtml { get; set; }
    public string? PageCss { get; set; }
    public string? PageJs { get; set; }
    public bool IsPublished { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Parent page in the nav tree; null = top level.</summary>
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
}

public sealed record PageSaveResult(bool Ok, int Id, ContentTrust StampedTrust, string? Error);

/// <summary>
/// Admin Page CRUD by (SiteId, Slug). The security-critical write is <see cref="SaveAsync"/>: it stamps
/// BodyTrust from the author's principal via <see cref="PageAuthoring"/> (Author iff the
/// Cms.AuthorRawMarkup claim is present, else Untrusted) so inline JS only runs for trusted authors.
/// Slug uniqueness is pre-checked (friendly error) with a DbUpdateException backstop. Delete is SOFT
/// (IsDeleted) — rows are never hard-deleted, so authored history and references never dangle.
/// </summary>
public interface IPageAdminService
{
    Task<IReadOnlyList<PageSummary>> ListAsync(CancellationToken ct = default);
    Task<PageEditModel?> GetAsync(int id, CancellationToken ct = default);
    Task<PageSaveResult> SaveAsync(PageEditModel model, ClaimsPrincipal author, CancellationToken ct = default);
    Task<bool> SetPublishedAsync(int id, bool published, CancellationToken ct = default);
    Task<bool> SetEnabledAsync(int id, bool enabled, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Re-home a page in the nav tree: set its parent (null = top level) and sort order. Rejects a move
    /// that would make a page its own ancestor (cycle guard), so the tree can never become malformed.
    /// </summary>
    Task<bool> MoveAsync(int id, int? parentId, int sortOrder, CancellationToken ct = default);
}

public sealed class PageAdminService(IDbContextFactory<CmsDbContext> dbFactory) : IPageAdminService
{
    public async Task<IReadOnlyList<PageSummary>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Pages.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Slug)
            .Select(p => new PageSummary(p.Id, p.Slug, p.Title, p.Kind, p.IsPublished, p.Enabled, p.BodyTrust,
                p.ParentId, p.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<PageEditModel?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.Pages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : new PageEditModel
        {
            Id = p.Id, Slug = p.Slug, Title = p.Title, ThemeKey = p.ThemeKey, ThemeVersion = p.ThemeVersion,
            Kind = p.Kind, BodyHtml = p.BodyHtml, PageCss = p.PageCss, PageJs = p.PageJs,
            IsPublished = p.IsPublished, Enabled = p.Enabled, ParentId = p.ParentId, SortOrder = p.SortOrder,
        };
    }

    public async Task<PageSaveResult> SaveAsync(PageEditModel model, ClaimsPrincipal author, CancellationToken ct = default)
    {
        var slug = (model.Slug ?? "").Trim().Trim('/').ToLowerInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var site = await db.Sites.FirstOrDefaultAsync(s => s.IsDefault, ct) ?? await db.Sites.FirstOrDefaultAsync(ct);
        var siteId = site?.Id;

        // Slug uniqueness pre-check (friendly error before the DB unique index fires).
        if (await db.Pages.AnyAsync(p => p.SiteId == siteId && p.Slug == slug && p.Id != model.Id, ct))
            return new PageSaveResult(false, model.Id, default, $"A page with slug '{(slug.Length == 0 ? "(home)" : slug)}' already exists.");

        var (trust, authoredBy) = PageAuthoring.Stamp(author);
        var now = DateTime.UtcNow;

        Page page;
        if (model.Id == 0)
        {
            page = new Page { SiteId = siteId, CreatedUtc = now };
            db.Pages.Add(page);
        }
        else
        {
            var found = await db.Pages.FirstOrDefaultAsync(p => p.Id == model.Id, ct);
            if (found is null) return new PageSaveResult(false, model.Id, default, "Page not found.");
            page = found;
        }

        page.Slug = slug;
        page.Title = model.Title;
        page.ThemeKey = string.IsNullOrWhiteSpace(model.ThemeKey) ? null : model.ThemeKey.Trim();
        page.ThemeVersion = model.ThemeVersion;
        page.Kind = model.Kind;
        page.BodyHtml = model.BodyHtml;
        page.PageCss = model.PageCss;
        page.PageJs = model.PageJs;
        page.IsPublished = model.IsPublished;
        page.Enabled = model.Enabled;
        page.ParentId = model.ParentId == model.Id ? null : model.ParentId;   // never self-parent
        page.SortOrder = model.SortOrder;
        page.BodyTrust = trust;                 // write-time trust stamp
        page.AuthoredByUserId = authoredBy;
        page.AuthorTrustVersion += 1;           // epoch bump
        page.ModifiedUtc = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new PageSaveResult(false, page.Id, trust, "Could not save — the slug may already be in use.");
        }
        return new PageSaveResult(true, page.Id, trust, null);
    }

    public async Task<bool> SetPublishedAsync(int id, bool published, CancellationToken ct = default) => await FlagAsync(id, ct, p => p.IsPublished = published);
    public async Task<bool> SetEnabledAsync(int id, bool enabled, CancellationToken ct = default) => await FlagAsync(id, ct, p => p.Enabled = enabled);

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default) =>
        await FlagAsync(id, ct, p => { p.IsDeleted = true; p.DeletedUtc = DateTime.UtcNow; });

    public async Task<bool> MoveAsync(int id, int? parentId, int sortOrder, CancellationToken ct = default)
    {
        if (parentId == id) return false;   // a page cannot be its own parent
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return false;

        // Cycle guard: walk up from the proposed parent — if we reach `id`, the move would make the page
        // its own ancestor. (Bounded walk so a pre-existing bad row can never spin forever.)
        var cursorId = parentId;
        for (var hops = 0; cursorId is int cid && hops < 256; hops++)
        {
            if (cid == id) return false;
            cursorId = await db.Pages.Where(p => p.Id == cid).Select(p => p.ParentId).FirstOrDefaultAsync(ct);
        }

        page.ParentId = parentId;
        page.SortOrder = sortOrder;
        page.ModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> FlagAsync(int id, CancellationToken ct, Action<Page> mutate)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return false;
        mutate(page);
        page.ModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
