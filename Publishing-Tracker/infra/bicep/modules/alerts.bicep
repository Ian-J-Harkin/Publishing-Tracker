targetScope = 'resourceGroup'

param tags object
param actionGroupName string
param apiWebAppResourceId string
param alertEmailReceiver string = ''

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmailReceiver)) {
  name: actionGroupName
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'ptalerts'
    enabled: true
    emailReceivers: [
      {
        name: 'ops-email'
        emailAddress: alertEmailReceiver
        useCommonAlertSchema: true
      }
    ]
  }
}

resource appServiceHttp5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-api-http5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alert when API returns elevated HTTP 5xx responses.'
    severity: 2
    enabled: true
    scopes: [
      apiWebAppResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'http5xx-threshold'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    autoMitigate: true
    actions: !empty(alertEmailReceiver)
      ? [
          {
            actionGroupId: actionGroup.id
          }
        ]
      : []
  }
}

output metricAlertId string = appServiceHttp5xxAlert.id
output actionGroupId string = !empty(alertEmailReceiver) ? actionGroup.id : ''
