// Cost-optimized Bicep template for RhinoMCP development/testing

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Base name for all resources')
@minLength(3)
@maxLength(20)
param baseName string = 'rhinomcp'

// SignalR Service - Free Tier (20 connections, 20K messages/day)
resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: '${baseName}-signalr'
  location: location
  sku: {
    name: 'Free_F1'  // Free tier for development/testing
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
    ]
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
  }
}

// Storage Account for connection codes
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: '${replace(baseName, '-', '')}storage'
  location: location
  sku: {
    name: 'Standard_LRS'  // Lowest cost option
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// App Service Plan - Free Tier
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${baseName}-plan'
  location: location
  sku: {
    name: 'F1'  // Free tier
  }
  kind: 'linux'
  properties: {
    reserved: true  // Required for Linux
  }
}

// Web App for Python
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: '${baseName}-app'
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'PYTHON|3.10'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
        {
          name: 'AZURE_SIGNALR_CONNECTION_STRING'
          value: signalR.listKeys().primaryConnectionString
        }
        {
          name: 'AZURE_STORAGE_CONNECTION_STRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'SIGNALR_HUB_URL'
          value: 'https://${signalR.properties.hostNamePrefix}.service.signalr.net/client/?hub=rhinomcp'
        }
        {
          name: 'CONNECTION_REGISTRY_URL'
          value: 'https://${baseName}-app.azurewebsites.net/api/'
        }
      ]
    }
    httpsOnly: true
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Output the Web App URL and SignalR connection string
output webAppUrl string = 'https://${baseName}-app.azurewebsites.net'
output signalRUrl string = 'https://${signalR.properties.hostNamePrefix}.service.signalr.net'
