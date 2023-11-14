param name string
param location string = resourceGroup().location
param sku string = 'standard'
param replicaCount int = 1
param partitionCount int = 1
@description('Applicable only for SKUs set to standard3. You can set this property to enable a single, high density partition that allows up to 1000 indexes, which is much higher than the maximum indexes allowed for any other SKU.')
@allowed([
  'default'
  'highDensity'
])
param hostingMode string = 'default'


resource search 'Microsoft.Search/searchServices@2020-08-01' = {
  name: name
  location: location
  sku: {
    name: sku
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
    hostingMode: hostingMode
  }
}

output name string = name
