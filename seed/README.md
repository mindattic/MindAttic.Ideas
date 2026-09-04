# seed/

`mindattic-site.ideabundle` — the MindAttic site as content: **53 pages, 86 component-metadata rows,
7 settings and 34 media payloads** in one file. This is the artifact that turns an empty deployment
into the real site.

```pwsh
dotnet run --project src/MindAttic.Ideas.Blazor -- --seed core                        # schema + the 53 library .ideas
dotnet run --project src/MindAttic.Ideas.Blazor -- --import-content seed/mindattic-site.ideabundle
```

Re-runnable: pages reconcile on uid then slug (so the baseline seed's own pages are **adopted**, not
duplicated) and media is matched by SHA-256, so a second import moves no bytes. Add `--dry-run` to see
what it would do first ([A34](../docs/AMENDMENTS.md#MAI-A34)).

**Why the payloads are in here rather than left to regenerate.** Most of the images are reproducible
— `tools/shoot` recaptures them from the source repos — but not all: the Prose Hub screenshot came
off a clipboard, and a re-capture produces different bytes anyway, so it gets a new uid and every page
referencing the old one would need regenerating. A seed that only half-restores is not a seed. The
file is ~18 MB and that is the price of it actually working on a clone.

This is also the natural **Day Zero baseline** for a showroom sandbox
([A36](../docs/AMENDMENTS.md#MAI-A36)): resetting one means dropping its content and importing this
scoped to that site.

## Refreshing it

```pwsh
dotnet run --project src/MindAttic.Ideas.Blazor -- --export-content seed/mindattic-site.ideabundle
```

Verified by restoring into an empty database before committing — 48 pages created, 5 adopted, 34
media uploaded, and every `/_media/{uid}` on the restored pages answering 200. Do that check again if
you refresh it; an unverified seed is worse than none.
