// MindAttic.Ideas — Azure infrastructure.
//
// One App Service, one SQL database, one storage account, one Key Vault. That is the whole estate:
// the CMS is a single deployment that hosts many pages (MAI-§1), so there is nothing per-page here.
//
// Everything is passwordless. The web app gets a system-assigned managed identity and reaches SQL,
// Blob Storage and Key Vault through it — no connection-string password, no storage key, no client
// secret anywhere in the repo, in CI, or in app settings (HOUSE-LAW-3).
//
// Deploy with infra/provision.ps1, which runs this and then does the two things Bicep cannot:
// create the SQL contained user for the app's identity, and seed the Security secrets.

targetScope = 'resourceGroup'

@description('Name of the web app. Becomes <appName>.azurewebsites.net, so it must be globally unique.')
@minLength(3)
@maxLength(40)
param appName string = 'mindattic-ideas'

@description('Azure region for every resource.')
param location string = resourceGroup().location

@description('App Service plan SKU. B1 is the cheapest tier with Always On, which a CMS needs so the first request after idle is not a cold start.')
@allowed(['B1', 'B2', 'S1', 'P0v3'])
param appServicePlanSku string = 'B1'

@description('Object ID of the Entra principal that administers SQL (you). Get it with: az ad signed-in-user show --query id -o tsv')
param sqlAdminObjectId string

@description('UPN or display name of that same principal — shown in the portal as the SQL admin.')
param sqlAdminLogin string

@description('SQL database SKU. Basic is 2GB and about five dollars a month; GP_S_Gen5_1 is serverless and auto-pauses.')
param sqlDatabaseSku string = 'Basic'

@description('Tag applied to every resource so the whole estate can be found and costed together.')
param projectTag string = 'MindAttic.Ideas'

var suffix = uniqueString(resourceGroup().id)
// Storage account names allow no hyphens and cap at 24 characters, so the app name is squashed and
// clipped before the uniqueness suffix is appended.
var nameBase = toLower(replace(appName, '-', ''))
var shortBase = substring(nameBase, 0, min(length(nameBase), 11))
var storageName = '${shortBase}${suffix}'
var keyVaultName = 'kv-${shortBase}-${substring(suffix, 0, 6)}'
var sqlServerName = 'sql-${shortBase}-${substring(suffix, 0, 6)}'
var sqlDatabaseName = 'MindAtticIdeas'
var mediaContainerName = 'media'
var dataProtectionContainerName = 'dp-keys'
var dataProtectionKeyName = 'dp-protect'

var commonTags = {
  project: projectTag
  managedBy: 'infra/main.bicep'
}

// Built-in role definition IDs. These GUIDs are stable across every Azure tenant.
var roleStorageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var roleKeyVaultCryptoUser = '12338af0-0e69-4776-bea7-57ae8d297424'
var roleKeyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var roleKeyVaultCryptoOfficer = '14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
var roleKeyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

// ---------------------------------------------------------------------------------------------
// Storage — media blobs and the Data Protection key ring.
// ---------------------------------------------------------------------------------------------

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  tags: commonTags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    // No anonymous access: media is served through a short-lived SAS minted by the app (MAI-A31),
    // so the container never needs to be public.
    allowBlobPublicAccess: false
    // Shared keys off — the app authenticates with its managed identity, and a SAS is user-delegation
    // signed. Leaving key auth on would leave a credential nobody intends to use lying around.
    allowSharedKeyAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    cors: {
      corsRules: []
    }
  }
}

resource mediaContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: mediaContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: dataProtectionContainerName
  properties: {
    publicAccess: 'None'
  }
}

// ---------------------------------------------------------------------------------------------
// Key Vault — the Data Protection key-encryption key, plus the auth Security bucket.
// ---------------------------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: commonTags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    // Purge protection is deliberately ON: the Data Protection KEK lives here, and losing it
    // invalidates every issued auth cookie and every protected payload at once.
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

// The deploying principal needs Crypto Officer to create the key below, and Secrets Officer so
// provision.ps1 can seed the Security bucket afterwards.
resource deployerCryptoOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, sqlAdminObjectId, roleKeyVaultCryptoOfficer)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultCryptoOfficer)
    principalId: sqlAdminObjectId
    principalType: 'User'
  }
}

resource deployerSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, sqlAdminObjectId, roleKeyVaultSecretsOfficer)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultSecretsOfficer)
    principalId: sqlAdminObjectId
    principalType: 'User'
  }
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: dataProtectionKeyName
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: ['wrapKey', 'unwrapKey']
  }
  dependsOn: [
    deployerCryptoOfficer
  ]
}

// ---------------------------------------------------------------------------------------------
// SQL — Entra-only authentication, so there is no server password to store or rotate.
// ---------------------------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: commonTags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      tenantId: subscription().tenantId
      principalType: 'User'
      azureADOnlyAuthentication: true
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: commonTags
  sku: {
    name: sqlDatabaseSku
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
  }
}

// App Service outbound IPs are not fixed, so the app reaches SQL through the Azure-services
// bypass rather than an IP allow-list. The 0.0.0.0 start/end pair is the documented sentinel for it.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ---------------------------------------------------------------------------------------------
// App Service.
// ---------------------------------------------------------------------------------------------

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${appName}'
  location: location
  tags: commonTags
  sku: {
    name: appServicePlanSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: commonTags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServicePlanSku != 'F1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      // Blazor Server holds a SignalR circuit per visitor; without affinity a reconnect can land on
      // another instance and drop the circuit.
      webSocketsEnabled: true
      healthCheckPath: '/_health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // First boot discovers every citizen, seeds the CMS and installs 51 bundled .idea packages
          // through the real install path -- against a 5-DTU Basic database that is minutes of work,
          // and the default 230s start limit kills the container mid-seed. Later boots are fast
          // because seeding is idempotent, but the first one has to be allowed to finish.
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '1800'
        }
        {
          // Entra-authenticated, passwordless. Microsoft.Data.SqlClient picks up the app's managed
          // identity through DefaultAzureCredential semantics.
          name: 'ConnectionStrings__Ideas'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        }
        {
          name: 'DataProtection__BlobUri'
          value: '${storage.properties.primaryEndpoints.blob}${dataProtectionContainerName}/ideas-keys.xml'
        }
        {
          name: 'DataProtection__KeyVaultKeyId'
          value: dataProtectionKey.properties.keyUriWithVersion
        }
        {
          name: 'Media__Provider'
          value: 'azure'
        }
        {
          name: 'Media__Azure__BlobServiceUri'
          value: storage.properties.primaryEndpoints.blob
        }
        {
          name: 'Media__Azure__ContainerName'
          value: mediaContainerName
        }
        {
          name: 'Media__Azure__SignedUrlMinutes'
          value: '60'
        }
        // The auth Security bucket. MindAttic.Authentication fail-closes without these. The secret
        // VALUES are generated into Key Vault by infra/provision.ps1; only the references live here,
        // because siteConfig.appSettings is authoritative -- anything added out-of-band would be
        // wiped by the next template deployment.
        {
          name: 'MindAttic__Vault__Security__pepper.v1'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/pepper-v1)'
        }
        {
          name: 'MindAttic__Vault__Security__bootstrap-token'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/bootstrap-token)'
        }
        {
          name: 'MindAttic__Vault__Security__reset-token-key'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/reset-token-key)'
        }
        {
          name: 'MindAttic__Vault__Security__dp-kek'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/dp-kek)'
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------------------------
// Role assignments for the app's managed identity.
// ---------------------------------------------------------------------------------------------

resource appStorageAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, webApp.id, roleStorageBlobDataContributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleStorageBlobDataContributor)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Wrap/unwrap only — the app protects the Data Protection key ring with this key but never reads it.
resource appKeyVaultCrypto 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, webApp.id, roleKeyVaultCryptoUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultCryptoUser)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Needed so the @Microsoft.KeyVault(...) app settings for the auth Security bucket resolve.
resource appKeyVaultSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, webApp.id, roleKeyVaultSecretsUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultSecretsUser)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------

output webAppName string = webApp.name
output webAppHostName string = webApp.properties.defaultHostName
output webAppPrincipalId string = webApp.identity.principalId
output storageAccountName string = storage.name
output blobServiceUri string = storage.properties.primaryEndpoints.blob
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabaseName
