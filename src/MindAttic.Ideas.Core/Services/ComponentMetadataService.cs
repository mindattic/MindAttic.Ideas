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
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
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
        await db.SaveChangesAsync(ct);
    }
}
