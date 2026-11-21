targetScope = 'resourceGroup'

param location string
param tags object
param appServicePlanName string
param apiWebAppName string
param agentWebAppName string
param applicationInsightsConnectionString string
param keyVaultUri string

resource servicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: 'B1'
    tier: 'Basic'
    size: 'B1'
    family: 'B'
    capacity: 1
  }
  kind: 'app'
  properties: {
    reserved: false
  }
}

resource apiWebApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiWebAppName
  location: location
  tags: tags
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: servicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'ConnectionStrings__NeonConnection'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/NeonConnection/)'
        }
        {
          name: 'Jwt__Key'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/JwtKey/)'
        }
        {
          name: 'Jwt__Issuer'
          value: 'PublishingTracker.Api'
        }
        {
          name: 'Jwt__Audience'
          value: 'PublishingTracker.Api'
        }
      ]
    }
  }
}

resource agentWebApp 'Microsoft.Web/sites@2023-12-01' = {
  name: agentWebAppName
  location: location
  tags: tags
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: servicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'ConnectionStrings__NeonConnection'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/NeonConnection/)'
        }
        {
          name: 'Jwt__Key'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/JwtKey/)'
        }
        {
          name: 'Jwt__Issuer'
          value: 'PublishingTracker.Api'
        }
        {
          name: 'Jwt__Audience'
          value: 'PublishingTracker.Api'
        }
        {
          name: 'AIProvider__McpEndpoint'
          value: 'https://${apiWebApp.properties.defaultHostName}/mcp'
        }
        {
          name: 'AIProvider__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/AIProviderApiKey/)'
        }
      ]
    }
  }
}

output appServicePlanId string = servicePlan.id
output webAppName string = apiWebApp.name
output defaultHostName string = apiWebApp.properties.defaultHostName
output principalId string = apiWebApp.identity.principalId
output webAppResourceId string = apiWebApp.id
output agentWebAppName string = agentWebApp.name
output agentDefaultHostName string = agentWebApp.properties.defaultHostName
output agentPrincipalId string = agentWebApp.identity.principalId
output agentWebAppResourceId string = agentWebApp.id
