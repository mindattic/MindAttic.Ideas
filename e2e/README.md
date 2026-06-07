# MindAttic.Ideas — Cypress E2E

End-to-end coverage of the core goal flow:

> admin logs in → opens admin → **uploads a compiled `.idea`** (added to the library) → **creates a page**
> that references it by a `{{tag}}` → the page **renders the widget** (no missing-content placeholder).

Spec: [`cypress/e2e/admin-widget-flow.cy.js`](cypress/e2e/admin-widget-flow.cy.js). Fixture:
`cypress/fixtures/MindAttic.Ideas.Widget.Tooltip.V1.idea` (packed from `MindAttic.Ideas.Library`).

## Run it

These tests drive a **running** CMS — they don't start one. Run the app on a **dev port + dev DB** so your
normal instance is untouched (per the established dev recipe):

```pwsh
# 1) start the CMS (separate port + DB; from the MindAttic.Ideas repo root)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS        = "https://localhost:7207"
$env:ConnectionStrings__Ideas = "Server=(localdb)\MSSQLLocalDB;Database=MindAtticIdeas_E2E;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/MindAttic.Ideas.Web
#   on first run the bundled library/*.idea seed into the catalog automatically.

# 2) in another shell: install + run Cypress (from this e2e/ folder)
npm install
$env:CYPRESS_BASE_URL       = "https://localhost:7207"
$env:CYPRESS_ADMIN_USER     = "admin"
$env:CYPRESS_ADMIN_PASSWORD = "<the admin password>"   # bootstrap admin from the Vault Security:bootstrap-token
npm run cy:run                                          # or: npm run cy:open
```

## Config (all overridable via env)

| Env var | Default | Meaning |
|---|---|---|
| `CYPRESS_BASE_URL` | `https://localhost:7207` | the running CMS |
| `CYPRESS_ADMIN_USER` | `admin` | admin username |
| `CYPRESS_ADMIN_PASSWORD` | _(none — required)_ | admin password |
| `CYPRESS_LOGIN_PATH` | `/account/login` | login page path |

## Notes
- The bootstrap admin is seeded with `MustChangePassword`; if the run hits the change-password step, complete
  it once (or seed a known password) so the session command can authenticate.
- Selectors are intentionally resilient (labels + button text). If the admin markup changes materially, update
  `cypress/support/commands.js` and the spec.
