#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes a standalone Release copy of MindAttic.Ideas to C:\Apps\Ideas\ when the source has
    changed, and optionally launches it. The one script for deploying and launching Ideas.

.DESCRIPTION
    Without -Force this is a fast no-op unless the deployed exe, run.bat or deploy.bat is missing,
    or a source file under this repo is newer than the deployed exe.

    When it does publish:
    1. Stops any running MindAttic.Ideas.Blazor.exe.
    2. With -Force, removes bin/obj for the Blazor, Core and Rendering projects (a clean rebuild).
       BUILD cache only: the LocalDB database (MindAtticIdeas, shared with the dev server), the Data
       Protection key ring under %APPDATA%\MindAttic\DataProtection\Ideas\, and any media already
       uploaded under C:\Apps\Ideas\media\ are never touched, so content and logins survive a redeploy.
    3. Repacks the first-party library (library\tools\pack-all.ps1 -Install) into the host's library\
       folder, then dotnet publish (Release, framework-dependent) -> C:\Apps\Ideas\. Library citizens keep
       their whole-number version while being edited, and boot seeding installs with allowOverride:false,
       so run.bat also points IDEAS_DROPBOX at the deployed library\ - the Development-only path that
       installs through the admin-upload code path WITH override, so edited packages actually replace
       the installed ones.
    4. Writes C:\Apps\Ideas\run.bat, which pins ASPNETCORE_URLS to a port distinct from the dev
       server (5229/7207) and sets ASPNETCORE_ENVIRONMENT=Development - Program.cs only runs the
       LocalDB migration and skips its production-only Azure Data Protection (Blob + Key Vault)
       requirement in Development, same convention IdiotProof.Blazor uses for its own deployed copy.
       The Release build itself is unaffected: the debug-only auth bypass is gated on build
       Configuration, not on ASPNETCORE_ENVIRONMENT, so it never ships here regardless.
    5. Writes C:\Apps\Ideas\deploy.bat (the desktop shortcut target), which runs this script before
       every launch, then starts run.bat and opens the browser. If the source repo is missing
       (moved/deleted), it warns and launches whatever is already deployed.

.PARAMETER Force
    Republish with a clean bin/obj rebuild even if nothing looks changed.

.PARAMETER Launch
    After deploying, start the deployed copy (run.bat) and open the browser.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy.ps1
    powershell -ExecutionPolicy Bypass -File deploy.ps1 -Force
    powershell -ExecutionPolicy Bypass -File deploy.ps1 -Launch
#>
param(
    [switch]$Force,
    [switch]$Launch,
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$LaunchArgs = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir   = $PSScriptRoot
$proj      = Join-Path $repoDir 'src\MindAttic.Ideas.Blazor\MindAttic.Ideas.Blazor.csproj'
$out       = 'C:\Apps\Ideas'
$exeName   = 'MindAttic.Ideas.Blazor.exe'
$exe       = Join-Path $out $exeName
$url       = 'http://localhost:5280'
$runBat    = Join-Path $out 'run.bat'
$deployBat = Join-Path $out 'deploy.bat'

$sourceExtensions = @('.cs', '.csproj', '.xaml', '.razor', '.cshtml', '.css', '.js', '.json',
                      '.props', '.targets', '.resx')
# publish\ is a gitignored local publish output, not source.
$skipDirs = @('bin', 'obj', '.git', '.vs', 'node_modules', 'publish', 'TestResults')

# Walk that never descends into build output or VCS folders; stops at the first newer source file.
function Find-NewerSource([string]$dir, [datetime]$since) {
    foreach ($item in Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue) {
        if ($item.PSIsContainer) {
            if ($skipDirs -contains $item.Name) { continue }
            if (Find-NewerSource $item.FullName $since) { return $true }
        } elseif (($sourceExtensions -contains $item.Extension.ToLowerInvariant()) -and
                  $item.LastWriteTimeUtc -gt $since) {
            Write-Host "  Changed: $($item.FullName)" -ForegroundColor DarkGray
            return $true
        }
    }
    return $false
}

function Test-NeedsPublish {
    foreach ($f in @($exe, $runBat, $deployBat)) { if (-not (Test-Path $f)) { return $true } }
    return (Find-NewerSource $repoDir (Get-Item $exe).LastWriteTimeUtc)
}

New-Item -ItemType Directory -Force -Path $out | Out-Null

if (-not $Force -and -not (Test-NeedsPublish)) {
    Write-Host '  Up to date - nothing to publish.' -ForegroundColor DarkGray
} else {
    # ── Stop running instance ──────────────────────────────────────────────
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

    # ── Clear build cache (-Force only) ────────────────────────────────────
    # bin/obj only - never the LocalDB database, the Data Protection key ring under
    # %APPDATA%\MindAttic\DataProtection\Ideas\, or media already deployed under $out\media\.
    if ($Force) {
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
    }

    # ── Repack the library into the host ───────────────────────────────────
    Write-Host ''
    Write-Host '  Packing library citizens into the host...' -ForegroundColor Cyan
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoDir 'library\tools\pack-all.ps1') -Sign -Install | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Library pack failed (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # ── Publish ────────────────────────────────────────────────────────────
    Write-Host ''
    Write-Host "  Publishing $proj" -ForegroundColor Cyan
    Write-Host "    -> $exe"

    dotnet publish $proj `
        --configuration Release `
        --no-self-contained `
        --output $out | Out-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Publish failed (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    if (-not (Test-Path $exe)) {
        Write-Host "Publish completed but $exeName not found at $exe." -ForegroundColor Red
        exit 1
    }
    # An incremental publish can copy an unchanged apphost with its old timestamp; stamp it so the
    # next run's change check compares against this deploy.
    (Get-Item $exe).LastWriteTime = Get-Date

    # ── Write run wrapper ──────────────────────────────────────────────────
    @(
        '@echo off',
        'cd /d "%~dp0"',
        "set ASPNETCORE_URLS=$url",
        'set ASPNETCORE_ENVIRONMENT=Development',
        'set "IDEAS_DROPBOX=%~dp0library"',
        "`"%~dp0$exeName`""
    ) | Set-Content $runBat -Encoding ascii

    # ── Write self-redeploying launcher (desktop shortcut target) ─────────
    # Runs this script (a fast no-op unless source changed) before every launch. Falls back to the
    # existing deployed copy (with a warning) if the repo has moved, rather than refusing to start.
    $deployPs1Path = Join-Path $repoDir 'deploy.ps1'
    @(
        '@echo off',
        'title MindAttic.Ideas',
        "rem Generated by $deployPs1Path - edits here are overwritten.",
        "set `"DEPLOY_PS1=$deployPs1Path`"",
        'if exist "%DEPLOY_PS1%" (',
        '    powershell -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_PS1%"',
        '    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.',
        ') else (',
        '    echo Source repo not found at "%DEPLOY_PS1%" - launching existing build without redeploying.',
        ')',
        "echo  Starting MindAttic.Ideas ($url)...",
        "start `"MindAttic.Ideas`" `"%~dp0run.bat`"",
        'timeout /t 3 /nobreak >nul',
        "start `"`" `"$url`""
    ) | Set-Content $deployBat -Encoding ascii
    # deploy.bat replaces the old generated launch.bat.
    $oldLaunch = Join-Path $out 'launch.bat'
    if (Test-Path $oldLaunch) { Remove-Item $oldLaunch -Force -ErrorAction SilentlyContinue }

    Write-Host ''
    Write-Host '  Published successfully.' -ForegroundColor Green
    Write-Host "    Exe    : $exe" -ForegroundColor Gray
    Write-Host "    Run    : $runBat ($url)" -ForegroundColor Gray
    Write-Host "    Launch : $deployBat" -ForegroundColor Gray
    Write-Host ''
}

if ($Launch) {
    Write-Host '  Launching...' -ForegroundColor Cyan
    Start-Process $runBat
    Start-Sleep -Seconds 3
    Start-Process $url
}
exit 0
