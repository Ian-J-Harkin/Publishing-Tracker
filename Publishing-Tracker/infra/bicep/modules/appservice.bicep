targetScope = 'resourceGroup'

param location string
param tags object
param appServicePlanName string
param apiWebAppName string
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
          name: 'DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/DefaultConnection/)'
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
