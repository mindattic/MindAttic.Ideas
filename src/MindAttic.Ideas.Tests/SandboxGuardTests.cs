using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// Showroom mode contains a routine that deletes a site's content on a timer. That is a loaded gun
/// pointed at the real site, so these tests exist to make one sentence true: <b>the main site is
/// never reset, under any combination of flags, from any direction.</b>
/// <para>
/// The guarantee is deliberately redundant. <see cref="ISiteAdminService"/> refuses to create the
/// dangerous state at write time, and <see cref="ISandboxService.Gate"/> refuses it again at reset
/// time — so neither a bad row written directly in SQL nor a future caller that skips the admin
/// service can wipe production.
/// </para>
/// </summary>
[TestFixture]
public class SandboxGuardTests
{
    private static CmsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CmsDbContext>()
            .UseInMemoryDatabase("sandbox_" + Guid.NewGuid().ToString("N")).Options);

    private static Site MainSite() => new()
    {
        Key = "main", Name = "MindAttic", IsDefault = true, CreatedUtc = DateTime.UtcNow,
    };

    private static Site Showroom(int graceMinutes = 10) => new()
    {
        Key = "showroom", Name = "Showroom", IsDefault = false, IsSandbox = true,
        ResetPolicy = SandboxService.WhenIdle, IdleGraceMinutes = graceMinutes,
        CreatedUtc = DateTime.UtcNow,
    };

    // ---- the one that must never fail ------------------------------------------------------

    [Test]
    public void TheDefaultSiteIsNeverResettable_EvenIfItIsSomehowFlaggedAsASandbox()
    {
        // The worst case: someone sets IsSandbox on the main site directly in the database, past
        // every write-time check. The gate must STILL refuse.
        var rogue = new Site
        {
            Key = "main", Name = "MindAttic", IsDefault = true,
            IsSandbox = true, ResetPolicy = SandboxService.WhenIdle,
        };

        var gate = new SandboxService(NewDb()).Gate(rogue);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Allowed, Is.False);
            Assert.That(gate.Refusal, Is.EqualTo(SandboxRefusal.IsDefaultSite),
                "the default check must come FIRST and independently of the sandbox flag");
            Assert.That(gate.Explanation, Does.Contain("never reset"));
        });
    }

    [Test]
    public async Task DueForReset_NeverIncludesTheDefaultSite()
    {
        await using var db = NewDb();
        db.Sites.AddRange(
            new Site { Key = "main", IsDefault = true, IsSandbox = true, ResetPolicy = SandboxService.WhenIdle },
            Showroom());
        await db.SaveChangesAsync();

        var due = await new SandboxService(db).DueForResetAsync(DateTime.UtcNow);

        Assert.That(due.Select(s => s.Key), Is.EqualTo(new[] { "showroom" }),
            "the sweep must re-gate every candidate, not trust its own query predicate");
    }

    [Test]
    public async Task TheDefaultSiteCannotBePutIntoShowroomMode()
    {
        await using var db = NewDb();
        var main = MainSite();
        db.Sites.Add(main);
        await db.SaveChangesAsync();

        var (ok, error) = await new SiteAdminService(db).SetSandboxAsync(main.Id, true, SandboxService.WhenIdle, 10);

        Assert.Multiple(async () =>
        {
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("default site"));
            Assert.That((await db.Sites.SingleAsync()).IsSandbox, Is.False, "and nothing was written");
        });
    }

    [Test]
    public async Task AShowroomSiteCannotBePromotedToDefault()
    {
        // The same danger reached from the opposite direction: making the self-wiping site the one
        // that answers every unclaimed host.
        await using var db = NewDb();
        db.Sites.AddRange(MainSite(), Showroom());
        await db.SaveChangesAsync();
        var showroom = await db.Sites.SingleAsync(s => s.Key == "showroom");

        var (ok, error) = await new SiteAdminService(db).MakeDefaultAsync(showroom.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Showroom mode"));
            Assert.That((await db.Sites.SingleAsync(s => s.IsDefault)).Key, Is.EqualTo("main"));
        });
    }

    // ---- the gate's other refusals ----------------------------------------------------------

    [Test]
    public void AnOrdinarySiteIsNotResettable()
    {
        var gate = new SandboxService(NewDb()).Gate(new Site { Key = "ryandebraal", IsDefault = false });
        Assert.Multiple(() =>
        {
            Assert.That(gate.Allowed, Is.False);
            Assert.That(gate.Refusal, Is.EqualTo(SandboxRefusal.NotASandbox));
        });
    }

    [Test]
    public void ASandboxWithNoResetPolicyIsNotResettable()
    {
        var gate = new SandboxService(NewDb()).Gate(new Site { Key = "s", IsSandbox = true, ResetPolicy = null });
        Assert.Multiple(() =>
        {
            Assert.That(gate.Allowed, Is.False);
            Assert.That(gate.Refusal, Is.EqualTo(SandboxRefusal.NoResetPolicy),
                "a sandbox nobody asked to be wiped is not wiped");
        });
    }

    [Test]
    public void AProperShowroomSitePassesTheGate()
    {
        var gate = new SandboxService(NewDb()).Gate(Showroom());
        Assert.Multiple(() =>
        {
            Assert.That(gate.Allowed, Is.True, gate.Explanation);
            Assert.That(gate.Refusal, Is.EqualTo(SandboxRefusal.None));
        });
    }

    // ---- idle detection ----------------------------------------------------------------------

    [Test]
    public async Task ALiveSessionHoldsOffTheReset()
    {
        await using var db = NewDb();
        db.Sites.Add(Showroom(graceMinutes: 10));
        var now = DateTime.UtcNow;
        db.AuthSessions.Add(new MindAttic.Authentication.Entities.AuthSession
        {
            Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), CreatedUtc = now.AddMinutes(-30),
            LastSeenUtc = now.AddMinutes(-1), AbsoluteExpiryUtc = now.AddHours(8),
        });
        await db.SaveChangesAsync();

        var due = await new SandboxService(db).DueForResetAsync(now);

        Assert.That(due, Is.Empty, "someone is mid-demo — resetting under them is the failure that matters");
    }

    [Test]
    public async Task OnceTheGracePeriodPasses_TheShowroomIsDue()
    {
        await using var db = NewDb();
        db.Sites.Add(Showroom(graceMinutes: 10));
        var now = DateTime.UtcNow;
        db.AuthSessions.Add(new MindAttic.Authentication.Entities.AuthSession
        {
            Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), CreatedUtc = now.AddHours(-2),
            LastSeenUtc = now.AddMinutes(-11), AbsoluteExpiryUtc = now.AddHours(8),
        });
        await db.SaveChangesAsync();

        var due = await new SandboxService(db).DueForResetAsync(now);

        Assert.That(due.Select(s => s.Key), Is.EqualTo(new[] { "showroom" }));
    }

    [Test]
    public async Task ARevokedSessionDoesNotHoldOffTheReset()
    {
        // Logging off is the signal to flush back to Day Zero.
        await using var db = NewDb();
        db.Sites.Add(Showroom(graceMinutes: 1));
        var now = DateTime.UtcNow;
        db.AuthSessions.Add(new MindAttic.Authentication.Entities.AuthSession
        {
            Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), CreatedUtc = now.AddHours(-1),
            LastSeenUtc = now, AbsoluteExpiryUtc = now.AddHours(8), RevokedUtc = now,
        });
        await db.SaveChangesAsync();

        var due = await new SandboxService(db).DueForResetAsync(now);

        Assert.That(due.Select(s => s.Key), Is.EqualTo(new[] { "showroom" }));
    }
}
