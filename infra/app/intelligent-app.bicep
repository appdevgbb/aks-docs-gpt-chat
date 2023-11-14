param openAiAccountName string
param cognitiveSearchAccountName string
param AzureOpenAiChatDeploymentName string
param AzureOpenAiEmbeddingDeploymentName string

param name string
param location string = resourceGroup().location
param tags object = {}


param allowedOrigins array = []
param applicationInsightsName string = ''
param appServicePlanId string
param appSettings object = {}
param keyVaultName string
param serviceName string = 'api'
param storageAccountName string

var cognitiveServiceSettings = {
  AZURE_COGNITIVE_SEARCH_ENDPOINT: 'https://${cognitiveSearchAccountName}.search.windows.net/'
  AZURE_COGNITIVE_SEARCH_APIKEY: cognitiveSearch.listAdminKeys().primaryKey
  
  AzureOpenAiEndpoint: 'https://${openAiAccountName}.openai.azure.com/'
  AzureOpenAiKey: openAi.listKeys().key1
  AzureOpenAiChatDeploymentName: AzureOpenAiChatDeploymentName
  AzureOpenAiEmbeddingDeploymentName: AzureOpenAiEmbeddingDeploymentName
}


resource openAi 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: openAiAccountName
}

resource cognitiveSearch 'Microsoft.Search/searchServices@2020-08-01' existing = {
  name: cognitiveSearchAccountName
}

module api '../core/host/functions.bicep' = {
  name: '${serviceName}-functions-dotnet-isolated-module'
  params: {
    name: name
    location: location
    tags: union(tags, { 'azd-service-name': serviceName })
    allowedOrigins: allowedOrigins
    alwaysOn: false
    appSettings: union(appSettings, cognitiveServiceSettings)
    applicationInsightsName: applicationInsightsName
    appServicePlanId: appServicePlanId
    keyVaultName: keyVaultName
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '7.0'
    storageAccountName: storageAccountName
    scmDoBuildDuringDeployment: false
  }
}

output SERVICE_API_IDENTITY_PRINCIPAL_ID string = api.outputs.identityPrincipalId
output SERVICE_API_NAME string = api.outputs.name
output SERVICE_API_URI string = api.outputs.uri
