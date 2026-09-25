#Requires -Version 5.1
<#
.SYNOPSIS
    Shuts down any running MindAttic.Ideas, clears the build cache, rebuilds, and publishes a
    standalone Release copy to C:\Apps\Ideas\ — same process Automata, JobHunt (tools\deploy.ps1)
    and IdiotProof.Blazor (tools\publish-all.ps1) use, so a double-click always runs current source
    rather than a stale build.

.DESCRIPTION
    1. Stops any running MindAttic.Ideas.Blazor.exe.
    2. Removes bin/obj for the Blazor, Core and Rendering projects — a clean rebuild, not an
       incremental one. BUILD cache only: the LocalDB database (MindAtticIdeas, shared with the dev
       server), the Data Protection key ring under %APPDATA%\MindAttic\DataProtection\Ideas\, and any
       media already uploaded under C:\Apps\Ideas\media\ are never touched, so content and logins
       survive a redeploy.
    3. Repacks the first-party library (library\tools\pack-all.ps1 -Install) into the host's library\
       folder, then dotnet publish (Release, framework-dependent) -> C:\Apps\Ideas\. Library citizens keep
       their whole-number version while being edited, and boot seeding installs with allowOverride:false,
       so run.bat also points IDEAS_DROPBOX at the deployed library\ — the Development-only path that
       installs through the admin-upload code path WITH override, so edited packages actually replace
       the installed ones.
    4. Writes C:\Apps\Ideas\run.bat, which pins ASPNETCORE_URLS to a port distinct from the dev
       server (5229/7207) and sets ASPNETCORE_ENVIRONMENT=Development — Program.cs only runs the
       LocalDB migration and skips its production-only Azure Data Protection (Blob + Key Vault)
       requirement in Development, same convention IdiotProof.Blazor uses for its own deployed copy.
       The Release build itself is unaffected: the debug-only auth bypass is gated on build
       Configuration, not on ASPNETCORE_ENVIRONMENT, so it never ships here regardless.
    5. Writes C:\Apps\Ideas\launch.bat, which calls this deploy.ps1 (by its baked-in source path)
       before every launch, then starts run.bat and opens the browser. If the source repo is missing
       (moved/deleted), it warns and launches whatever is already deployed.

.PARAMETER Launch
    After publishing, immediately start the freshly published copy (run.bat) and open the browser.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir = Split-Path $PSScriptRoot   # tools\ -> MindAttic.Ideas\
$proj    = Join-Path $repoDir 'src\MindAttic.Ideas.Blazor\MindAttic.Ideas.Blazor.csproj'
$out     = 'C:\Apps\Ideas'
$exeName = 'MindAttic.Ideas.Blazor.exe'
$url     = 'http://localhost:5280'

# ── Stop running instance ──────────────────────────────────────────────────
Write-Host ''
Write-Host '  Stopping running instance...' -ForegroundColor Yellow
$procs = Get-Process 'MindAttic.Ideas.Blazor' -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force
    Write-Host "    Stopped ($(@($procs).Count) process(es))" -ForegroundColor DarkYellow
    Start-Sleep -Milliseconds 800
} else {
    Write-Host '    Nothing running.' -ForegroundColor DarkGray
}

# ── Clear build cache ──────────────────────────────────────────────────────
# bin/obj only — never the LocalDB database, the Data Protection key ring under
# %APPDATA%\MindAttic\DataProtection\Ideas\, or media already deployed under $out\media\.
Write-Host ''
Write-Host '  Clearing build cache (bin/obj)...' -ForegroundColor Cyan
foreach ($rel in @('src\MindAttic.Ideas.Blazor', 'src\MindAttic.Ideas.Core', 'src\MindAttic.Ideas.Rendering')) {
    $projRoot = Join-Path $repoDir $rel
    if (-not (Test-Path $projRoot)) { continue }
    foreach ($d in @((Join-Path $projRoot 'bin'), (Join-Path $projRoot 'obj'))) {
        if (Test-Path $d) {
            try { Remove-Item -Recurse -Force $d -ErrorAction Stop }
            catch { Write-Host "    (could not fully remove $d : $($_.Exception.Message))" -ForegroundColor DarkYellow }
        }
    }
}

# ── Repack the library into the host ───────────────────────────────────────
Write-Host ''
Write-Host '  Packing library citizens into the host...' -ForegroundColor Cyan
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoDir 'library\tools\pack-all.ps1') -Sign -Install | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "Library pack failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

# ── Publish ─────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host "  Publishing $proj" -ForegroundColor Cyan
Write-Host "    -> $out\$exeName"

dotnet publish $proj `
    --configuration Release `
    --no-self-contained `
    --output $out | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$exe = Join-Path $out $exeName
if (-not (Test-Path $exe)) {
    Write-Host "Publish completed but $exeName not found at $exe." -ForegroundColor Red
    exit 1
}

# ── Write run wrapper ───────────────────────────────────────────────────────
$runBatPath = Join-Path $out 'run.bat'
@(
    '@echo off',
    'cd /d "%~dp0"',
    "set ASPNETCORE_URLS=$url",
    'set ASPNETCORE_ENVIRONMENT=Development',
    'set "IDEAS_DROPBOX=%~dp0library"',
    "`"%~dp0$exeName`""
) | Set-Content $runBatPath -Encoding ascii

# ── Write self-redeploying launcher ─────────────────────────────────────────
# Always redeploys from source before launching, so a double-click can never run a stale build.
# $PSScriptRoot here is THIS script's own folder — baked into the batch file as an absolute path so
# it still resolves no matter where launch.bat itself is invoked from. Falls back to launching the
# existing deployed copy (with a warning) if that path is missing, rather than refusing to start.
$deployPs1Path = Join-Path $PSScriptRoot 'deploy.ps1'
$launchBatPath = Join-Path $out 'launch.bat'
@(
    '@echo off',
    'title MindAttic.Ideas',
    "set `"DEPLOY_PS1=$deployPs1Path`"",
    'if exist "%DEPLOY_PS1%" (',
    '    echo Redeploying latest build from source...',
    '    powershell -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_PS1%"',
    '    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.',
    ') else (',
    '    echo Source repo not found at "%DEPLOY_PS1%" - launching existing build without redeploying.',
    ')',
    "echo  Starting MindAttic.Ideas ($url)...",
    "start `"MindAttic.Ideas`" `"%~dp0run.bat`"",
    'timeout /t 3 /nobreak >nul',
    "start `"`" `"$url`""
) | Set-Content $launchBatPath -Encoding ascii

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Published successfully.' -ForegroundColor Green
Write-Host "    Exe    : $exe" -ForegroundColor Gray
Write-Host "    Run    : $runBatPath ($url)" -ForegroundColor Gray
Write-Host "    Launch : $launchBatPath" -ForegroundColor Gray
Write-Host ''

if ($Launch) {
    Write-Host '  Launching...' -ForegroundColor Cyan
    Start-Process $runBatPath
    Start-Sleep -Seconds 3
    Start-Process $url
}
