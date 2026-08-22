targetScope = 'subscription'

@description('Environment name, for example dev or prod.')
param environment string

@description('Azure location for all resources.')
param location string = 'westeurope'

@description('Resource group name to create/use for this environment.')
param resourceGroupName string

@description('Common tags applied to all resources.')
param tags object = {
  application: 'publishing-tracker'
  environment: environment
  managedBy: 'bicep'
}

@description('App Service plan name.')
param appServicePlanName string

@description('API Web App name.')
param apiWebAppName string

@description('Log Analytics workspace name.')
param logAnalyticsWorkspaceName string

@description('Application Insights resource name.')
param applicationInsightsName string

@description('Azure SQL logical server name.')
param sqlServerName string

@description('Azure SQL database name.')
param sqlDatabaseName string

@description('SQL administrator login name.')
param sqlAdminLogin string

@secure()
@description('SQL administrator password. Pass via secure CI secret.')
param sqlAdminPassword string

@allowed([
  'Enabled'
  'Disabled'
])
@description('SQL public network access setting. Use Disabled only with private connectivity in place.')
param sqlPublicNetworkAccess string = 'Enabled'

@description('If true, adds SQL firewall rule to allow Azure services (0.0.0.0).')
param sqlAllowAzureServices bool = true

@description('Optional single client IP allowed through SQL firewall.')
param sqlAllowedClientIp string = ''

@description('Azure Key Vault name.')
param keyVaultName string

@description('Action group name for alert notifications.')
param alertActionGroupName string = 'ag-publishing-tracker-${environment}'

@description('Optional alert recipient email for infra notifications.')
param alertEmailReceiver string = ''

resource infraResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring-${environment}'
  scope: infraResourceGroup
  params: {
    location: location
    tags: tags
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    applicationInsightsName: applicationInsightsName
  }
}

module sql './modules/sql.bicep' = {
  name: 'sql-${environment}'
  scope: infraResourceGroup
  params: {
    location: location
    tags: tags
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    sqlPublicNetworkAccess: sqlPublicNetworkAccess
    allowAzureServices: sqlAllowAzureServices
    allowedClientIp: sqlAllowedClientIp
  }
}

module keyVault './modules/keyvault.bicep' = {
  name: 'keyvault-${environment}'
  scope: infraResourceGroup
  params: {
    location: location
    tags: tags
    keyVaultName: keyVaultName
    tenantId: tenant().tenantId
  }
}

module appService './modules/appservice.bicep' = {
  name: 'appservice-${environment}'
  scope: infraResourceGroup
  params: {
    location: location
    tags: tags
    appServicePlanName: appServicePlanName
    apiWebAppName: apiWebAppName
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    keyVaultUri: keyVault.outputs.keyVaultUri
  }
}

module keyVaultAccess './modules/keyvault-rbac.bicep' = {
  name: 'keyvault-rbac-${environment}'
  scope: infraResourceGroup
  params: {
    keyVaultName: keyVaultName
    principalId: appService.outputs.principalId
  }
}

module alerts './modules/alerts.bicep' = {
  name: 'alerts-${environment}'
  scope: infraResourceGroup
  params: {
    tags: tags
    actionGroupName: alertActionGroupName
    apiWebAppResourceId: appService.outputs.webAppResourceId
    alertEmailReceiver: alertEmailReceiver
  }
}

output resourceGroup string = infraResourceGroup.name
output apiHostName string = appService.outputs.defaultHostName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output keyVaultRoleAssignmentId string = keyVaultAccess.outputs.roleAssignmentId
output appServiceHttp5xxAlertId string = alerts.outputs.metricAlertId
