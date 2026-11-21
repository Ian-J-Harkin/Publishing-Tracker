targetScope = 'resourceGroup'

param location string
param tags object
param keyVaultName string
param tenantId string

@secure()
@description('Optional Neon PostgreSQL connection string stored as secret NeonConnection.')
param neonConnectionString string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForTemplateDeployment: false
    enabledForDiskEncryption: false
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }

  resource neonConnectionSecret 'secrets@2023-07-01' = if (!empty(neonConnectionString)) {
    name: 'NeonConnection'
    properties: {
      value: neonConnectionString
    }
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
