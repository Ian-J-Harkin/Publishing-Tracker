targetScope = 'resourceGroup'

@description('User-assigned managed identity name.')
param identityName string

@description('Azure location.')
param location string

@description('Resource tags.')
param tags object

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

output id string = userAssignedIdentity.id
output principalId string = userAssignedIdentity.properties.principalId
output clientId string = userAssignedIdentity.properties.clientId
