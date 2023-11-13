targetScope = 'subscription'

@minLength(3)
@maxLength(3)
@description('A prefix string to generate a short unique hash used in all resources.')
param prefix string = 'gbb'

@minLength(4)
@description('Primary location for all resources')
param location string = 'eastus2'

@description('Resource Group to deploy all resources to')
param resourceGroupName string = '${prefix}-rg'

@description('Name of the App Service Plan to create')
param appServicePlanName string = ''

@description('Name of the API Service to create')
param apiServiceName string = ''

// @description('Name of the front end UI app to create')
// param frontEndName string = 'frontend'

var resourceToken = toLower(uniqueString(subscription().id, prefix, location))

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
}

// Create an App Service Plan to group applications under the same payment plan and SKU
module appServicePlan './app/app-service-plan.bicep' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    name: !empty(appServicePlanName) ? appServicePlanName : resourceToken
    location: location
    sku: {
      name: 'B1'
    }
  }
}

module appService './app/app-service.bicep' = {
  name: 'appservice'
  scope: rg
  dependsOn: [
    appServicePlan
  ]
  params: {
    name: !empty(apiServiceName) ? apiServiceName : resourceToken
    location: location
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '7.0'
    tags: {
      'azd-service-name': apiServiceName
    }
  }
}

// Data outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
