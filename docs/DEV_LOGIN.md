# Dev login on localhost — MindAttic.Ideas Admin

> Owner question: "Determine a safe way to log into Admin on localhost that does NOT decrease the
> security of MindAttic.Authentication; e.g. will an un-committed `.env` file be enough?"

## Decision (decisive)

**Use the library's existing bootstrap-token path. Do NOT add a `.env` file or any second secret store.**

A developer logs into Admin on localhost exactly the way production seeds its first admin: the
`AuthBootstrapper` seeds the `admin` account on an empty DB using the Vault `Security:bootstrap-token`
read from the **uncommitted, machine-local** file `%APPDATA%\MindAttic\Security\providers.json`. The dev
signs in at `/login` as `admin` / `<bootstrap-token>`, is force-redirected to change the password, and the
token is then dead. This is byte-for-byte the proven StreetSamurai recipe — no app-specific dev backdoor.

This is **already provisioned on this machine**: `%APPDATA%\MindAttic\Security\providers.json` exists and
contains `pepper.v1`, `bootstrap-token`, `reset-token-key`, `dp-kek`. The only Ideas-side wiring needed is to
add `"Security"` to the `AddMindAtticVaultFiles` bucket list (the default list excludes it — see
`MindAttic.Vault` `MindAtticConfigurationSource.Buckets`).

## How the dev logs in (exact steps)

1. **Provision the dev Security bucket** (one-time per dev machine; already done here). Create
   `%APPDATA%\MindAttic\Security\providers.json`:
   ```jsonc
   {
     "pepper.v1":       "<base64 of 32 random bytes>",
     "bootstrap-token": "<a strong >=12-char string>",
     "reset-token-key": "<base64 of 32 random bytes>",
     "dp-kek":          "<base64 of 32 random bytes>"
   }
   ```
   Generate a 32-byte base64 value:
   ```powershell
   [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
   ```
   This file lives under `%APPDATA%`, **outside the repo** — it physically cannot be committed.
2. **Run the app** (`dotnet run` in `MindAttic.Ideas.Web`). On an empty DB, startup migrates the auth schema,
   then `AuthBootstrapper.SeedAdminAsync()` creates `admin` with `MustChangePassword = MustEnrollMfa = true`
   and `PasswordHash = Argon2id(bootstrap-token + pepper)`. (MFA is OFF for now —
   `MindAttic:Auth:Mfa:RequireForAdmin = false` — so `MustEnrollMfa` does not gate `MaPolicies.Admin`.)
3. **Sign in** at `https://localhost:<port>/login` as `admin` / `<bootstrap-token>`.
4. The forced-step middleware redirects to `/account/change-password`. Set a real password (>=12 chars,
   HIBP-checked). After this, the bootstrap-token no longer logs anyone in — the admin's hash is its own.

No standing backdoor remains after step 4: the bootstrap-token is only ever the *initial* password and is
inert once changed. `SeedAdminAsync` is a no-op on every subsequent boot (it short-circuits if any user
exists).

## Why the rejected options are worse

Checklist applied to each: (1) adds a second secret store / trust domain, (2) weakens fail-closed posture,
(3) risks committing a secret, (4) bypasses MustChangePassword/MFA, (5) diverges from prod.

### (ii) Uncommitted `.env` file holding an admin password — REJECTED
- **Second secret store / trust domain (FAIL).** Ideas already standardized on MindAttic.Vault as the single
  local source of truth (`%APPDATA%\MindAttic\<bucket>`). `.NET User Secrets is retired in this ecosystem`,
  and the playbook explicitly makes `%APPDATA%` the one place. A `.env` introduces a *parallel* secret
  channel that the Vault chain doesn't own — exactly the fragmentation the architecture forbids.
- **.NET does not read `.env` natively (FAIL).** You'd add `DotNetEnv` or hand-roll a parser, i.e. new code
  and a new dependency purely to create a weaker path. The bootstrap-token path needs zero new code.
- **Commit risk (FAIL).** `.env` lives *inside the repo working tree*. The Ideas `.gitignore` has **no
  `.env` entry today** — a fresh `git add -A` would stage it. `%APPDATA%\...\providers.json` cannot be
  staged because it isn't in the tree at all. This is the single biggest reason to reject `.env`.
- **Bypasses MustChangePassword (FAIL).** A raw admin password in `.env` would have to be injected as a
  pre-set credential, sidestepping the forced-change-on-first-login invariant — the dev would run with a
  static, file-resident password indefinitely. The bootstrap-token forces a change and then dies.
- **Diverges from prod (FAIL).** Prod has no `.env`; it seeds via bootstrap-token from Key Vault. A `.env`
  dev path is a different code path that prod never exercises, so "works on my machine" tells you nothing
  about the real sign-in flow.

### (iii) A dev-only auto-login / `DevAutoLoginMiddleware` backdoor — REJECTED (hardest no)
- This is the exact anti-pattern StreetSamurai's adoption **deleted** ("it carries a DevAutoLoginMiddleware
  backdoor and a hardcoded admin password that MUST die"). It bypasses the cookie pipeline, MustChangePassword,
  and (later) MFA entirely; it is trivially left enabled into prod by a config slip; it issues an authenticated
  principal with no credential at all. Fails every checklist item. Do not introduce one.

### (iv) Keeping Ideas' current `Ideas:AdminPassword` default `"ChangeMe!2026"` — REJECTED
- A hardcoded credential baked into `Program.cs`/config. Commits a secret (it's literally in source),
  bypasses MustChangePassword, and is deleted as part of this adoption anyway. The whole point of the
  library is to retire `SeedService` + `Ideas:AdminPassword`.

## Posture summary (why option i is fail-closed and prod-faithful)

| Concern | bootstrap-token (chosen) |
|---|---|
| Second secret store | **No** — same Vault `Security` bucket prod uses |
| Fail-closed | **Yes** — no `bootstrap-token` ⇒ can't seed; no `pepper.v1` ⇒ won't start |
| Commit risk | **None** — secret lives in `%APPDATA%`, outside the repo |
| MustChangePassword / MFA | **Honored** — seeded with both flags; forced-change before use |
| Dev/prod divergence | **None** — identical seed path, only the secret backend differs (file vs Key Vault) |

## Reset / re-seed for a clean test

Because Ideas is pre-release with no real author data, to re-test a clean first login: drop the dev DB
(`MindAtticIdeas` LocalDB) so the next boot re-runs migrate -> seed. (Re-keying `Page.AuthoredByUserId`
from the old `User.Id` string to `AuthUser.Id` Guid is moot — reset the dev DB rather than migrate it.)
Optionally rotate the dev `bootstrap-token` in `providers.json` between runs to mirror the prod
rotate-after-seed discipline, though on a single-dev box it isn't required.
