# Cost-Optimized Azure Setup for RhinoMCP (Using Existing Resource Group)

This document outlines how to deploy a cost-optimized Azure setup for your RhinoMCP remote connection using your existing resource group `rg-rhinomcp-remote-dev`.

## Setup Instructions

### 1. Run the Deployment Script

I've created a deployment script that will handle everything for you. The script:

1. Uses your existing resource group `rg-rhinomcp-remote-dev` 
2. Deploys all necessary Azure resources using free tiers where possible:
   - SignalR service (Free_F1 tier) - for real-time communication
   - Storage account (Standard_LRS) - for storing connection codes
   - App Service Plan (F1 free tier) - for hosting the server
   - Web App - for running the Python MCP server
3. Deploys the Python server code to Azure App Service
4. Sets up the connection code system

Before deploying, the script will show you what resources will be created and ask for your confirmation.

To run the deployment:

```bash
# Make sure the script is executable
chmod +x deploy.sh

# Run the script
./deploy.sh
```

### 2. Using the Connection Codes

After deployment is complete, you will see:

1. The Web App URL
2. The SignalR URL
3. A URL to view your connection codes

You can share these connection codes with Rhino users. They will:

1. Run the `mcpconnectazure` command in Rhino
2. Choose "Connection Code" option
3. Enter the code you shared with them

### 3. Free Tier Limitations

This setup uses Azure free tiers which have the following limitations:

1. **SignalR Free Tier**:
   - Limited to 20 concurrent connections
   - 20,000 messages per day

2. **App Service Free Tier**:
   - 60 minutes of compute per day
   - Shared infrastructure (may be slower)
   - No custom domain

These limitations should be fine for your testing with a few connections. If you need to upgrade later, modify the SKU values in the Bicep template.

## Troubleshooting

If you encounter any issues:

1. **Connection Problems**: Check that your Azure App Service is running. Visit the Azure portal and check the App Service status.

2. **Missing Connection Codes**: Wait a few minutes after deployment for the server to initialize and create codes.

3. **Additional Resources**: If the free tier limits are causing issues, modify the `main.bicep` file and update the SKU values:
   - For SignalR: Change from `Free_F1` to `Standard_S1`
   - For App Service: Change from `F1` to `B1` or higher
