param name string
param location string = resourceGroup().location
param tags object = {}

param customSubDomainName string = name

param deployments array

param kind string = 'OpenAI'
param publicNetworkAccess string = 'Disabled'
param sku object = {
  name: 'S0'
}

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': name })
  kind: kind
  properties: {
    customSubDomainName: customSubDomainName
    publicNetworkAccess: publicNetworkAccess
    networkAcls: {
      defaultAction: 'Deny'
    }
  }
  sku: sku
}


@batchSize(1)
resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = [for deployment in deployments: {
  parent: account
  name: deployment.name
  properties: {
    model: deployment.model
    raiPolicyName: contains(deployment, 'raiPolicyName') ? deployment.raiPolicyName : null
  }
  sku: contains(deployment, 'sku') ? deployment.sku : {
    name: 'Standard'
    capacity: 20
  }
}]

output openAiName string = account.name
output openAiEndpointUri string = '${account.properties.endpoint}openai/'
output account object = account
// output deploymentName string = deployment.name
