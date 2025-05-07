# Cost-Optimized Azure Infrastructure for RhinoMCP

This document outlines a cost-optimized Azure setup for testing your RhinoMCP remote connection. This version is designed for development/testing with 1-5 connections.

## Core Resources (Cost-Optimized)

1. **Azure App Service (Free Tier)** - Hosts the Python MCP server
2. **Azure SignalR Service (Free Tier)** - Enables real-time communication
3. **Azure Storage** - Stores connection codes for easy connection

## Estimated Monthly Cost
- **Total**: ~$0-1 per month for testing (just minimal storage costs)
- You may need to upgrade certain components for production use later

## Setup Instructions

### 1. Create Resource Group

Create a resource group following Azure naming conventions:

```bash
az group create --name rg-rhinomcp-remote-dev --location eastus
```

### 2. Create Azure Resources

Save the following Bicep template as `main.bicep` and deploy it:

```bicep
// Cost-optimized Bicep template for RhinoMCP development/testing
// Deploy with: az deployment group create --resource-group rg-rhinomcp-remote-dev --template-file main.bicep

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Base name for all resources')
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
          value: 'https://${webApp.properties.defaultHostName}/api/'
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
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output signalRUrl string = 'https://${signalR.properties.hostNamePrefix}.service.signalr.net'
```

### 3. Deploy the Template

Run this command in your terminal to deploy the resources:

```bash
# First extract the Bicep code to a file
cat > main.bicep << 'EOL'
// Copy the Bicep code from above and paste it here
EOL

# Deploy the resources
az login
az deployment group create --resource-group rg-rhinomcp-remote-dev --template-file main.bicep
```

### 4. Deploy the Python Server

After creating the resources:

1. Create a simplified `minimal-requirements.txt`:
   ```
   azure-signalr>=1.2.0
   azure-data-tables>=12.4.0
   fastapi>=0.95.0
   uvicorn>=0.21.1
   python-dotenv>=1.0.0
   aiohttp>=3.8.4
   requests>=2.28.2
   ```

2. Deploy using the Azure CLI:
   ```bash
   # Navigate to your server directory
   cd rhino_mcp_server
   
   # Create a deployment zip
   zip -r deployment.zip . -x "*.git*"
   
   # Deploy to App Service
   az webapp deployment source config-zip --resource-group rg-rhinomcp-remote-dev --name rhinomcp-app --src deployment.zip
   
   # Set the startup command
   az webapp config set --resource-group rg-rhinomcp-remote-dev --name rhinomcp-app --startup-file "python azure_server.py"
   ```

### 5. Connect with Rhino

1. Install the Rhino plugin as usual
2. Run the `mcpconnectazure` command in Rhino
3. When prompted, enter the connection code from your Azure app

## Limitations of Free Tier

This cost-optimized setup uses free tiers that have some limitations:

1. **SignalR Free Tier**:
   - Limited to 20 concurrent connections
   - 20,000 messages per day

2. **App Service Free Tier**:
   - 60 minutes of compute per day
   - Shared infrastructure (may be slower)
   - No custom domain
   - No auto-scaling

If you need to scale beyond these limits later, you can update the SKU parameters in the Bicep template:
- For SignalR: Change from `Free_F1` to `Standard_S1`
- For App Service: Change from `F1` to `B1` or higher

## Viewing Connection Codes

Access your connection codes at: `https://rhinomcp-app.azurewebsites.net/static/codes.html`
