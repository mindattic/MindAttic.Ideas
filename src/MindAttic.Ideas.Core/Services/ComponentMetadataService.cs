using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Services;

public sealed class ComponentMetadataService(IDbContextFactory<CmsDbContext> dbFactory) : IComponentMetadataStore
{
    public async Task<string?> GetAsync(Guid pageUid, string componentKey, string slotName = "main", CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ComponentMetadata
            .Where(m => m.PageUid == pageUid && m.ComponentKey == componentKey && m.SlotName == slotName)
            .Select(m => (string?)m.MetadataJson)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(Guid pageUid, string componentKey, string slotName, string metadataJson, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Retry once: two admin tabs saving simultaneously both see existing=null, both try to insert,
        // and the second SaveChangesAsync hits the UNIQUE index. On the retry the winner's row is visible.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var existing = await db.ComponentMetadata
                .FirstOrDefaultAsync(m => m.PageUid == pageUid && m.ComponentKey == componentKey && m.SlotName == slotName, ct);
            if (existing is null)
            {
                db.ComponentMetadata.Add(new ComponentMetadata
                {
                    PageUid = pageUid, ComponentKey = componentKey, SlotName = slotName,
                    MetadataJson = metadataJson, CreatedUtc = now, ModifiedUtc = now,
                });
            }
            else
            {
                existing.MetadataJson = metadataJson;
                existing.ModifiedUtc = now;
            }
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt == 0 && existing is null)
            {
                // Concurrent insert won the race — retry with a fresh context to load the winner's row.
            }
        }
    }
}
