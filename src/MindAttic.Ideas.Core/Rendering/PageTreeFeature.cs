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
            return Array.Empty<ChildPage>();
        }
    }

    public async Task<IReadOnlyList<ChildPageNode>> DescendantsTreeAsync(Guid pageId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var root = await db.Pages.FirstOrDefaultAsync(p => p.Uid == pageId && !p.IsDeleted, ct);
            if (root is null) return Array.Empty<ChildPageNode>();

            // Single query: load all published, enabled pages in this site, then build the tree in memory.
            var siteId = root.SiteId;
            var all = await db.Pages
                .Where(p => p.SiteId == siteId && p.IsPublished && p.Enabled && !p.IsDeleted)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
                .Select(p => new { p.Id, p.ParentId, p.Slug, p.Title })
                .ToListAsync(ct);

            var byParent = all.GroupBy(p => p.ParentId).ToDictionary(g => g.Key, g => g.ToList());

            IReadOnlyList<ChildPageNode> BuildNodes(int parentId)
            {
                if (!byParent.TryGetValue(parentId, out var kids)) return Array.Empty<ChildPageNode>();
                return kids.Select(k => new ChildPageNode(k.Slug, k.Title, BuildNodes(k.Id))).ToList();
            }

            return BuildNodes(root.Id);
        }
        catch
        {
            return Array.Empty<ChildPageNode>();
        }
    }
}
