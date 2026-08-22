targetScope = 'resourceGroup'

@description('Key Vault name to scope the role assignment to.')
param keyVaultName string

@description('Principal ID receiving Key Vault secret read access.')
param principalId string

@description('Role definition ID. Defaults to Key Vault Secrets User.')
param roleDefinitionId string = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

var assignmentName = guid(keyVault.id, principalId, roleDefinitionId)

resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: assignmentName
  scope: keyVault
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
  }
}

output roleAssignmentId string = keyVaultRoleAssignment.id
