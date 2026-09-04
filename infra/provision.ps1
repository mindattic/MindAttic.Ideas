<#
.SYNOPSIS
    Provisions the Azure estate for MindAttic.Ideas and wires up everything Bicep cannot.

.DESCRIPTION
    Three phases:

      1. Deploy infra/main.bicep  — App Service, SQL, Storage, Key Vault, managed identity, RBAC.
      2. Seed the auth Security bucket into Key Vault and point app settings at it. Values are
         generated here with a CSPRNG and never printed, never written to disk, never committed.
      3. Create the SQL contained user for the web app's managed identity and grant it the two
         roles it needs. The app itself is granted db_datareader/db_datawriter only — DDL belongs
         to the migrate job, not to the running site.

    Safe to re-run. Bicep is declarative, secrets are only generated when absent, and the SQL user
    creation is guarded by an existence check.

.PARAMETER ResourceGroup
    Resource group to deploy into. Created if it does not exist.

.PARAMETER Location
    Azure region. Defaults to centralus.

.PARAMETER AppName
    Web app name; becomes <AppName>.azurewebsites.net, so it must be globally unique.

.PARAMETER SqlDatabaseSku
    Basic (~$5/mo, 2GB) or GP_S_Gen5_1 (serverless, auto-pauses).

.PARAMETER WhatIf
    Show what the Bicep deployment would change, then stop without touching anything.

.EXAMPLE
    ./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas -WhatIf
    ./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string] $ResourceGroup,
    [string] $Location = 'centralus',
    [string] $AppName = 'mindattic-ideas',
    [ValidateSet('B1', 'B2', 'S1', 'P0v3')][string] $AppServicePlanSku = 'B1',
    [string] $SqlDatabaseSku = 'Basic'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$bicep = Join-Path $PSScriptRoot 'main.bicep'

function Write-Step($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

# --- Preflight -------------------------------------------------------------------------------

Write-Step 'Preflight'
if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'Azure CLI (az) is not on PATH.' }

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) { throw 'Not logged in. Run: az login' }
Write-Host "Subscription : $($account.name) ($($account.id))"
Write-Host "Signed in as : $($account.user.name)"

$signedInId = az ad signed-in-user show --query id -o tsv
if (-not $signedInId) { throw 'Could not resolve the signed-in user object id.' }
$signedInUpn = az ad signed-in-user show --query userPrincipalName -o tsv
if (-not $signedInUpn) { $signedInUpn = $account.user.name }
Write-Host "Object id    : $signedInId"

# --- Phase 1: infrastructure ------------------------------------------------------------------

Write-Step "Resource group '$ResourceGroup'"
az group create --name $ResourceGroup --location $Location --output none
Write-Host 'Ready.'

$deployMode = if ($WhatIfPreference) { 'what-if' } else { 'create' }
$deployArgs = @(
    'deployment', 'group', $deployMode,
    '--resource-group', $ResourceGroup,
    '--template-file', $bicep,
    '--parameters',
    "appName=$AppName",
    "location=$Location",
    "appServicePlanSku=$AppServicePlanSku",
    "sqlDatabaseSku=$SqlDatabaseSku",
    "sqlAdminObjectId=$signedInId",
    "sqlAdminLogin=$signedInUpn"
)

if ($WhatIfPreference) {
    Write-Step 'What-if (no changes will be made)'
    az @deployArgs
    Write-Host "`nWhat-if complete. Re-run without -WhatIf to apply." -ForegroundColor Yellow
    return
}

Write-Step 'Deploying infrastructure (a few minutes)'
$deployArgs += @('--name', "ideas-$(Get-Date -Format yyyyMMddHHmmss)", '--output', 'json')
$result = az @deployArgs | ConvertFrom-Json
if (-not $result) { throw 'Bicep deployment failed.' }

$out = $result.properties.outputs
$webAppName = $out.webAppName.value
$webAppHost = $out.webAppHostName.value
$keyVaultName = $out.keyVaultName.value
$sqlServerFqdn = $out.sqlServerFqdn.value
$sqlDatabase = $out.sqlDatabaseName.value
$blobServiceUri = $out.blobServiceUri.value

Write-Host "Web app   : https://$webAppHost"
Write-Host "Key Vault : $keyVaultName"
Write-Host "SQL       : $sqlServerFqdn/$sqlDatabase"
Write-Host "Blob      : $blobServiceUri"

# --- Phase 2: the auth Security bucket ----------------------------------------------------------

Write-Step 'Seeding the auth Security bucket into Key Vault'

# MindAttic.Authentication fail-closes without these (ConfigAuthSecrets.GetRequired). Key Vault
# secret names allow only alphanumerics and hyphens, so 'pepper.v1' is stored as 'pepper-v1' and the
# app setting maps it back to the dotted config key.
function New-RandomBase64([int] $bytes) {
    # RandomNumberGenerator.Fill is .NET Core only; Create()/GetBytes works on 5.1 and 7 alike.
    $buffer = New-Object byte[] $bytes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
    [Convert]::ToBase64String($buffer)
}

$secrets = @(
    @{ Vault = 'pepper-v1';       Config = 'pepper.v1';       Generate = { New-RandomBase64 32 } }
    @{ Vault = 'reset-token-key'; Config = 'reset-token-key'; Generate = { New-RandomBase64 32 } }
    @{ Vault = 'dp-kek';          Config = 'dp-kek';          Generate = { New-RandomBase64 32 } }
    # The bootstrap token is typed by a human once, at first sign-in, then rotated. Base64 padding
    # is stripped so it is not mistaken for a byte-valued secret.
    @{ Vault = 'bootstrap-token'; Config = 'bootstrap-token'; Generate = { (New-RandomBase64 24) -replace '[+/=]', '' } }
)

foreach ($secret in $secrets) {
    $exists = az keyvault secret show --vault-name $keyVaultName --name $secret.Vault --query id -o tsv 2>$null
    if ($exists) {
        Write-Host "  = $($secret.Vault) already present, left alone"
        continue
    }
    $value = & $secret.Generate
    az keyvault secret set --vault-name $keyVaultName --name $secret.Vault --value $value --output none
    Write-Host "  + $($secret.Vault) generated"
    $value = $null
}

Write-Step 'Pointing app settings at those secrets'
$settings = foreach ($secret in $secrets) {
    $uri = "https://$keyVaultName.vault.azure.net/secrets/$($secret.Vault)"
    "MindAttic__Vault__Security__$($secret.Config)=@Microsoft.KeyVault(SecretUri=$uri)"
}
az webapp config appsettings set --resource-group $ResourceGroup --name $webAppName `
    --settings @settings --output none
Write-Host 'Set.'

# --- Phase 3: the SQL contained user for the app's identity ------------------------------------

Write-Step 'Granting the web app read/write on SQL'

# The running site never issues DDL (Program.cs runs MigrateAsync in Development only); schema
# changes come from the CI migrate job. So the app identity gets datareader + datawriter and
# nothing more.
$tsql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$webAppName')
BEGIN
    CREATE USER [$webAppName] FROM EXTERNAL PROVIDER;
END;
ALTER ROLE db_datareader ADD MEMBER [$webAppName];
ALTER ROLE db_datawriter ADD MEMBER [$webAppName];
"@

$myIp = (Invoke-RestMethod -Uri 'https://api.ipify.org?format=json').ip
Write-Host "Opening the SQL firewall for this machine ($myIp)..."
$sqlServerName = $out.sqlServerName.value
az sql server firewall-rule create --resource-group $ResourceGroup --server $sqlServerName `
    --name 'provision-script' --start-ip-address $myIp --end-ip-address $myIp --output none

try {
    if (-not (Get-Module -ListAvailable -Name SqlServer)) {
        Write-Host 'Installing the SqlServer PowerShell module (current user)...'
        Install-Module SqlServer -Scope CurrentUser -Force -AllowClobber
    }
    Import-Module SqlServer

    $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
    Invoke-Sqlcmd -ServerInstance $sqlServerFqdn -Database $sqlDatabase -AccessToken $token -Query $tsql
    Write-Host "Granted db_datareader + db_datawriter to [$webAppName]."
}
finally {
    az sql server firewall-rule delete --resource-group $ResourceGroup --server $sqlServerName `
        --name 'provision-script' --output none 2>$null
    Write-Host 'Closed the temporary firewall rule.'
}

# --- Done ---------------------------------------------------------------------------------------

Write-Step 'Provisioned'
Write-Host @"
Estate is up. Remaining steps, in order:

  1. Apply the schema (the site has no tables yet):
         ./infra/migrate.ps1 -ResourceGroup $ResourceGroup -SqlServer $sqlServerFqdn -Database $sqlDatabase

  2. Deploy the app. Either let CI do it:
         gh secret set AZURE_WEBAPP_PUBLISH_PROFILE --repo mindattic/MindAttic.Ideas < profile.xml
         git push origin master
     or push once from here:
         dotnet publish src/MindAttic.Ideas.Blazor -c Release -o publish
         az webapp deploy --resource-group $ResourceGroup --name $webAppName --src-path publish --type zip

  3. Sign in at https://$webAppHost/ as 'admin' with the bootstrap token:
         az keyvault secret show --vault-name $keyVaultName --name bootstrap-token --query value -o tsv
     You will be forced to change it. ROTATE the Key Vault secret afterwards.

Full runbook: docs/DEPLOYMENT.md
"@ -ForegroundColor Green
