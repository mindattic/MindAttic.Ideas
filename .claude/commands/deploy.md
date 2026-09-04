Deploy MindAttic.Ideas via **MindAttic.Deploy** (sibling repo at `D:\Projects\MindAttic\MindAttic.Deploy`). MindAttic.Deploy is the source of truth for every MindAttic deploy; this command shims into it.

The deploy fires this repo's GitHub Actions workflow (`azure-deploy.yml`) by pushing `master`. The workflow builds Release, runs the full NUnit suite, applies the idempotent EF migration script to Azure SQL, lands the artifact on the `mindattic-ideas` App Service, and polls `/_health` until it answers 200.

Run this command and report the result:

```
powershell -NoProfile -ExecutionPolicy Bypass -Command "cd D:\Projects\MindAttic\MindAttic.Deploy; npm run deploy -- --app ideas"
```

It will:

1. Run the `dotnet-build` pre-deploy hook against `MindAttic.Ideas.Blazor.csproj` (`-c Release`) to catch compile errors locally before pushing.
2. `git -C ../MindAttic.Ideas push origin master` if local commits are ahead of remote, triggering the Actions workflow.
3. Print the Actions URL for monitoring: <https://github.com/mindattic/MindAttic.Ideas/actions/workflows/azure-deploy.yml>.

After running, summarize: which steps ran, what was pushed (or that there were no changes), and the Actions URL.

Notes:
- For a no-push rehearsal (build only, no push), append `--dry-run`.
- **The app entry ships `disabled: true`.** Until the Azure estate exists the command prints its note and exits 0 — it never half-fires. Stand the estate up with `./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas`, set the repo secrets, then flip `disabled: false` in `MindAttic.Deploy/projects.json`. Full runbook: `docs/DEPLOYMENT.md`.
- **Content does not need a deploy.** A page, widget or theme goes live by uploading its `.idea` through Admin; media goes in through Admin → Media or `--upload-media`. Deploy only when the engine itself changed.
