#!/bin/bash

# Terminal colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}RhinoMCP Azure Deployment Script (Using Existing Resource Group)${NC}"
echo "----------------------------------------------------------------"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Azure CLI not found. Please install it first:"
    echo "brew install azure-cli"
    exit 1
fi

# Login to Azure
echo -e "${YELLOW}Step 1: Logging in to Azure...${NC}"
az login

# Use existing resource group
echo -e "${YELLOW}Step 2: Using existing resource group 'rg-rhinomcp-remote-dev'...${NC}"
# Get the location of the existing resource group
location=$(az group show --name rg-rhinomcp-remote-dev --query location -o tsv)
echo -e "Using location: ${location}"

# Create Bicep file
echo -e "${YELLOW}Step 3: Creating Bicep template...${NC}"
cat > main.bicep << 'BICEP'
// Cost-optimized Bicep template for RhinoMCP development/testing

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
BICEP

# Deploy resources
echo -e "${YELLOW}Step 4: Deploying Azure resources (this may take a few minutes)...${NC}"
az deployment group create --resource-group rg-rhinomcp-remote-dev --template-file main.bicep

# Get deployment outputs
echo -e "${YELLOW}Step 5: Getting deployment outputs...${NC}"
webAppUrl=$(az deployment group show --resource-group rg-rhinomcp-remote-dev --name main --query properties.outputs.webAppUrl.value -o tsv)
signalRUrl=$(az deployment group show --resource-group rg-rhinomcp-remote-dev --name main --query properties.outputs.signalRUrl.value -o tsv)

# Create minimal requirements file
echo -e "${YELLOW}Step 6: Creating minimal requirements file...${NC}"
cat > minimal-requirements.txt << 'REQUIREMENTS'
azure-signalr>=1.2.0
azure-data-tables>=12.4.0
fastapi>=0.95.0
uvicorn>=0.21.1
python-dotenv>=1.0.0
aiohttp>=3.8.4
requests>=2.28.2
REQUIREMENTS

# Create deployment package
echo -e "${YELLOW}Step 7: Creating deployment package...${NC}"
cd rhino_mcp_server
cp ../minimal-requirements.txt requirements.txt
zip -r ../deployment.zip . -x "*.git*" -x "*.DS_Store"
cd ..

# Deploy to App Service
echo -e "${YELLOW}Step 8: Deploying server code to App Service...${NC}"
az webapp deployment source config-zip --resource-group rg-rhinomcp-remote-dev --name rhinomcp-app --src deployment.zip

# Set startup command
echo -e "${YELLOW}Step 9: Setting startup command...${NC}"
az webapp config set --resource-group rg-rhinomcp-remote-dev --name rhinomcp-app --startup-file "python azure_server.py"

# Done
echo -e "${GREEN}Deployment complete!${NC}"
echo -e "Web App URL: ${webAppUrl}"
echo -e "SignalR URL: ${signalRUrl}"
echo -e "Connection codes URL: ${webAppUrl}/static/codes.html"
echo -e ""
echo -e "${GREEN}To connect from Rhino:${NC}"
echo "1. Run the 'mcpconnectazure' command in Rhino"
echo "2. Choose the 'Connection Code' option"
echo "3. Enter a code from the codes URL above"
echo ""
echo "Enjoy using RhinoMCP Remote!"
