# Deploying MindAttic.Ideas to Azure

MindAttic.Ideas is a **single deployment** — one App Service, one app pool, one database
([BIBLE §1](BIBLE.md#MAI-§1)). Pages go live by uploading a `.idea`, not by redeploying, so this
runbook runs rarely: once to stand the estate up, then only when the engine itself changes.

Everything here is **passwordless**. The web app has a system-assigned managed identity and reaches
SQL, Blob Storage and Key Vault through it. There is no SQL password, no storage key and no client
secret in the repo, in CI, or in app settings ([HOUSE-LAW-3](../../MindAttic.HouseRules.md#HOUSE-LAW-3)).

---

## What gets created

`infra/main.bicep` provisions 16 resources:

| Resource | Why |
|---|---|
| App Service plan (B1 Linux) + web app | The single deployment. B1 is the cheapest tier with Always On, so the first request after idle is not a cold start. |
| Azure SQL server + `MindAtticIdeas` database | The CMS catalog, pages, media rows, users. Entra-only auth — the server has no SQL login at all. |
| Storage account, container `media` | Blob-backed media ([A31](AMENDMENTS.md#MAI-A31)). Private: no anonymous access, no shared keys. |
| Storage container `dp-keys` | The Data Protection key ring. |
| Key Vault + RSA key `dp-protect` | Wraps the key ring at rest. Purge protection is **on** — losing this key invalidates every issued auth cookie at once. |
| 5 role assignments | The app identity gets Storage Blob Data Contributor, Key Vault Crypto User and Key Vault Secrets User; the deployer gets Crypto Officer + Secrets Officer so it can create the key and seed secrets. |

Roughly **$18–20/month** at the defaults (B1 ≈ $13, SQL Basic ≈ $5, storage and Key Vault are
pennies). Pass `-SqlDatabaseSku GP_S_Gen5_1` for a serverless database that auto-pauses instead.

---

## First-time setup

### 1. Provision

```pwsh
az login
./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas -WhatIf   # look first
./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas
```

The script is re-runnable. It deploys the Bicep, then does the two things Bicep cannot:

- **Seeds the auth Security bucket into Key Vault.** MindAttic.Authentication fail-closes without
  `pepper.v1`, `bootstrap-token`, `reset-token-key` and `dp-kek`. They are generated with a CSPRNG,
  written straight to Key Vault, and never printed or written to disk. App settings reference them
  as `@Microsoft.KeyVault(SecretUri=…)`, which the config chain surfaces at
  `MindAttic:Vault:Security:<name>`. Existing secrets are left alone.
- **Creates the SQL contained user** for the app identity with `db_datareader` + `db_datawriter`
  **only**. The running site never issues DDL.

### 2. Apply the schema

The database is empty until you do this — `MigrateAsync` runs in Development only.

```pwsh
./infra/migrate.ps1 -ResourceGroup rg-mindattic-ideas -SqlServer sql-….database.windows.net
```

Generates an **idempotent** script (every migration guarded by its own `__EFMigrationsHistory`
check), authenticates with an Entra access token, opens a single-IP firewall rule and always removes
it again. Re-running is a no-op.

### 3. Deploy the app

Either push once from here:

```pwsh
dotnet publish src/MindAttic.Ideas.Blazor -c Release -o publish
az webapp deploy --resource-group rg-mindattic-ideas --name mindattic-ideas --src-path publish --type zip
```

…or hand it to CI (below), which is the steady state.

### 4. Sign in and rotate

```pwsh
az keyvault secret show --vault-name kv-… --name bootstrap-token --query value -o tsv
```

Sign in at `https://mindattic-ideas.azurewebsites.net/` as `admin` with that token. The account is
created `MustChangePassword`, so you are forced to set a real one. **Rotate the Key Vault secret
immediately afterwards** — it has served its purpose.

---

## Continuous deployment

`.github/workflows/azure-deploy.yml` runs on push to `master` and on manual dispatch, in three
gated stages:

1. **build** — restore, build Release, run the full NUnit suite, publish, and emit the idempotent
   migration script. A red test stops the deploy.
2. **migrate** — apply that script to Azure SQL under an Entra token, opening and closing a
   single-run firewall rule. Skippable via the `skip_migrate` dispatch input.
3. **deploy** — push the artifact, then poll `/_health` until it answers 200. Deploy runs when
   migrate is skipped, but never when migrate *ran and failed*: shipping code against a schema that
   did not apply is how you get a half-migrated production database.

### Required repo secrets

| Secret | Used by | How to get it |
|---|---|---|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | deploy | App Service → **Get publish profile**, paste the whole XML. |
| `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` | migrate | An app registration with a **federated credential** for this repo, granted Contributor on the resource group. |

If you would rather not set up OIDC, dispatch the workflow with `skip_migrate: true` and run
`infra/migrate.ps1` by hand whenever a migration is added. Everything else still works.

### Turning it on in MindAttic.Deploy

The `ideas` entry in `MindAttic.Deploy/projects.json → apps[]` ships `disabled: true`
([HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2) — a target is disabled with a note, never
deleted). Once the infra exists and the secrets are set, flip it to `disabled: false`; then
`npm run deploy -- --app ideas` pushes `master` and fires the workflow.

---

## The NuGet problem, and why `lib/local-packages/` is in git

Ideas references six private packages — `MindAttic.Vault` (V3+), `MindAttic.Legion`,
`MindAttic.Authentication`, `MindAttic.Media`, `MindAttic.Media.Azure` and
`MindAttic.Ideas.Page.Frontpage`. On a dev box those come from `C:\LocalNuGet` and `..\local-feed`.
**A GitHub runner has neither**, and NuGet tolerates a missing local source *silently* — so without
vendored copies the restore fails with a confusing `NU1101` about a package that plainly exists.

`lib/local-packages/` holds a git-tracked copy of each, and `nuget.config` lists it first so a dev
box and CI resolve identically. `.gitignore` excludes `*.nupkg` globally and re-includes this folder.

**When you bump a MindAttic package:** drop the new `.nupkg` in `lib/local-packages/` *and* update
the `PackageReference`. Old versions can stay; NuGet picks the one the csproj asks for.

---

## Configuration reference

Set as App Service application settings. `__` maps to `:` in the config chain.

| Setting | Value | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Turns off `MigrateAsync`, the dropbox auto-install, and the dev auth bypass. |
| `ConnectionStrings__Ideas` | `Server=tcp:…;Authentication=Active Directory Default;…` | No password. |
| `DataProtection__BlobUri` | `https://….blob.core.windows.net/dp-keys/ideas-keys.xml` | **Required in production** — the app throws without it. |
| `DataProtection__KeyVaultKeyId` | `https://….vault.azure.net/keys/dp-protect/<version>` | **Required in production.** |
| `Media__Provider` | `azure` | `local` keeps assets on the App Service filesystem, which does not survive a redeploy. |
| `Media__Azure__BlobServiceUri` | `https://….blob.core.windows.net/` | |
| `Media__Azure__ContainerName` | `media` | |
| `Media__Azure__SignedUrlMinutes` | `60` | SAS lifetime for `/_media/{uid}` redirects. |
| `MindAttic__Vault__Security__pepper.v1` | Key Vault reference | Argon2id pepper. |
| `MindAttic__Vault__Security__bootstrap-token` | Key Vault reference | First-admin seed; rotate after use. |
| `MindAttic__Vault__Security__reset-token-key` | Key Vault reference | Password-reset token signing key. |
| `MindAttic__Vault__Security__dp-kek` | Key Vault reference | |

---

## Getting content in

- **A page or widget** — upload the `.idea` through Admin. No redeploy ([BIBLE §1](BIBLE.md#MAI-§1)).
- **An image** — Admin → Media.
- **A video, or anything large** — `--upload-media`, which streams from disk straight into blob
  storage instead of crossing a SignalR circuit:
  ```pwsh
  dotnet run --project src/MindAttic.Ideas.Blazor -- --upload-media .\feature.mp4 --folder site
  ```
  Point it at production by exporting the same `Media__*` and `ConnectionStrings__Ideas` values
  locally. `/_media/{uid}` then 302s to a short-lived SAS and Azure serves the Range requests
  ([A31](AMENDMENTS.md#MAI-A31)).

---

## Troubleshooting

**App returns 500 immediately after deploy.** Almost always a missing required setting. Check the
log stream: `az webapp log tail -g rg-mindattic-ideas -n mindattic-ideas`. `DataProtection:BlobUri`
and `DataProtection:KeyVaultKeyId` throw by name; a missing Security secret throws
`Required auth secret '<name>' was not found`.

**`/_health` is 200 but every page 404s.** The catalog seeded but no pages exist — expected on a
brand-new database until you install content.

**Media 404s or 403s.** Confirm the app identity still holds Storage Blob Data Contributor. A SAS
is user-delegation signed, which needs that role; without it the signer declines and the endpoint
falls back to streaming, which then finds no bytes.

**Migrate job cannot reach SQL.** The firewall rule is per-run and torn down in an `always()` step.
If a run was killed mid-flight, delete the leftover `gh-<runid>` rule on the SQL server.

**`az webapp deploy` reports failure but the site is fine.** The CLI stops polling at ten minutes;
first boot installs 51 `.idea`s against a 5-DTU database and takes longer. Trust `/_health`, not the
CLI's verdict — the template sets `WEBSITES_CONTAINER_START_TIME_LIMIT=1800` so the container itself
is allowed to finish.

**Deployment 400s with rsync "Invalid argument" errors.** The zip was built by PowerShell's
`Compress-Archive`, which writes `\` path separators that Linux cannot unpack. Build the package with
forward slashes (`dotnet publish` then a zip tool that uses `/`).

**A secret you definitely set is "not found" on Linux.** App Service rewrites application-setting
names when injecting them as environment variables: hyphens are dropped and dots become underscores,
so `…Security__pepper.v1` arrives as `…Security__pepper_v1`. MindAttic.Authentication V4 matches
these by reducing both sides to letters and digits ([A33](AMENDMENTS.md#MAI-A33)); older versions
fail-closed on a secret that is genuinely present. Prefer alphanumeric setting names.

**App aborts at startup with a stack trace inside `ConfigurationBuilder`.** MindAttic.Vault below V3
threw when the host had no user profile, which on Linux is during host construction — SIGABRT before
any application code runs. V3 resolves a root on every OS instead ([VLT-A3](../../MindAttic.Vault/docs/AMENDMENTS.md)).
