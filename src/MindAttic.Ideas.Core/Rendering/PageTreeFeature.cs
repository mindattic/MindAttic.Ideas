using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// Host implementation of <see cref="IPageTree"/>: returns the published, enabled, non-deleted child
/// pages of a page (by its <see cref="Entities.ContentEntityBase.Uid"/>), ordered by SortOrder then Title.
/// Resolved by a Widget via <see cref="IRenderContext.TryGetFeature{T}"/>; the TableOfContents widget uses
/// it to render the current page's children — or nothing when there are none. Never throws into a render.
/// </summary>
public sealed class PageTreeFeature(IDbContextFactory<CmsDbContext> factory) : IPageTree
{
    public async Task<IReadOnlyList<ChildPage>> ChildrenOfAsync(Guid pageId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var parent = await db.Pages.FirstOrDefaultAsync(p => p.Uid == pageId && !p.IsDeleted, ct);
            if (parent is null) return Array.Empty<ChildPage>();

            return await db.Pages
                .Where(p => p.ParentId == parent.Id && p.IsPublished && p.Enabled && !p.IsDeleted)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
                .Select(p => new ChildPage(p.Slug, p.Title))
                .ToListAsync(ct);
        }
        catch
        {
            // A tree lookup must never break a render — degrade to "no children".
            return Array.Empty<ChildPage>();
        }
    }
}
