targetScope = 'resourceGroup'

param location string
param tags object
param sqlServerName string
param sqlDatabaseName string
param sqlAdminLogin string

@secure()
param sqlAdminPassword string

@allowed([
  'Enabled'
  'Disabled'
])
param sqlPublicNetworkAccess string = 'Enabled'

param allowAzureServices bool = true
param allowedClientIp string = ''

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: sqlPublicNetworkAccess
  }

  resource allowAzureServicesRule 'firewallRules@2023-08-01-preview' = if (sqlPublicNetworkAccess == 'Enabled' && allowAzureServices) {
    name: 'AllowAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }

  resource allowClientIpRule 'firewallRules@2023-08-01-preview' = if (sqlPublicNetworkAccess == 'Enabled' && !empty(allowedClientIp)) {
    name: 'AllowClientIp'
    properties: {
      startIpAddress: allowedClientIp
      endIpAddress: allowedClientIp
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
