# RhinoMCP Azure Deployment Guide

This guide will walk you through deploying the RhinoMCP server to Azure using the provided deployment scripts.

## Prerequisites

1. **Azure CLI**: Make sure Azure CLI is installed
   ```bash
   brew install azure-cli
   ```

2. **Azure Account**: You need an Azure account with access to create resources

## Deployment Steps

### Step 1: Login to Azure

```bash
az login
```

### Step 2: Run the Deployment Script

The deployment script will:
- Create all necessary Azure resources using free tiers where possible
- Deploy the Python server code to Azure App Service
- Configure the connection code system

```bash
cd rhinomcp-dist
chmod +x deploy.sh
./deploy.sh
```

The script will:
1. Show what resources will be created (using `az deployment group what-if`)
2. Ask for your confirmation before proceeding
3. Create the resources
4. Deploy the code to Azure App Service

### Step 3: Verify Deployment

After successful deployment, you'll see:
1. The Web App URL
2. The SignalR URL
3. A URL to view connection codes

Visit the connection codes URL to verify that your server is running correctly:
```
https://<baseName>-app.azurewebsites.net/static/codes.html
```

## Using the Connection

To connect from Rhino:
1. Install the RhinoMCP plugin in Rhino 8
2. Run the `mcpconnectazure` command in Rhino
3. Choose "Connection Code" option
4. Enter a code from your connection codes page

## Troubleshooting

### Server Not Starting
If the server doesn't start properly:
1. Check the App Service logs in the Azure portal
2. Verify that the startup command is correctly set to `python azure_server.py`

### Missing Connection Codes
If no connection codes appear:
1. Wait a few minutes for the tables to be created
2. Check that the storage account was properly created
3. Verify the app settings for `AZURE_STORAGE_CONNECTION_STRING` in the Azure portal

### Connection Issues
If Rhino cannot connect:
1. Verify that the SignalR service is running
2. Check that the connection codes are valid and not expired
3. Ensure that there are no network restrictions blocking connections

## Resource Limitations

This deployment uses Free tiers with these limitations:
- SignalR Free Tier: 20 concurrent connections, 20K messages/day
- App Service Free Tier: 60 minutes of compute per day, shared infrastructure

For production use, modify the SKU values in the Bicep template.
