targetScope = 'resourceGroup'

@description('Object ID (principalId) to assign the role to.')
param principalId string

@description('Role definition GUID, e.g. Key Vault Secrets User.')
param roleDefinitionId string

@description('Principal type for role assignment.')
@allowed([
  'ServicePrincipal'
  'User'
  'Group'
  'ForeignGroup'
  'Device'
])
param principalType string = 'ServicePrincipal'

var assignmentName = guid(resourceGroup().id, principalId, roleDefinitionId)

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: assignmentName
  scope: resourceGroup()
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
  }
}

output roleAssignmentId string = roleAssignment.id
