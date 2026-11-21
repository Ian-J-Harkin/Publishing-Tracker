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

@description('Agent Web App name.')
param agentWebAppName string = 'app-publishing-tracker-agent-${environment}'

@allowed([
  'gemini'
  'openrouter'
  'openai'
])
@description('Hosted AI provider type for the Agent service.')
param aiProviderType string = 'gemini'

@description('Log Analytics workspace name.')
param logAnalyticsWorkspaceName string

@description('Application Insights resource name.')
param applicationInsightsName string

@secure()
@description('Neon Serverless PostgreSQL connection string stored in Azure Key Vault. Pass via secure CI secret.')
param neonConnectionString string = ''

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

module keyVault './modules/keyvault.bicep' = {
  name: 'keyvault-${environment}'
  scope: infraResourceGroup
  params: {
    location: location
    tags: tags
    keyVaultName: keyVaultName
    tenantId: tenant().tenantId
    neonConnectionString: neonConnectionString
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
    agentWebAppName: agentWebAppName
    aiProviderType: aiProviderType
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

module agentKeyVaultAccess './modules/keyvault-rbac.bicep' = {
  name: 'keyvault-rbac-agent-${environment}'
  scope: infraResourceGroup
  params: {
    keyVaultName: keyVaultName
    principalId: appService.outputs.agentPrincipalId
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
output agentHostName string = appService.outputs.agentDefaultHostName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output keyVaultRoleAssignmentId string = keyVaultAccess.outputs.roleAssignmentId
output agentKeyVaultRoleAssignmentId string = agentKeyVaultAccess.outputs.roleAssignmentId
output appServiceHttp5xxAlertId string = alerts.outputs.metricAlertId
