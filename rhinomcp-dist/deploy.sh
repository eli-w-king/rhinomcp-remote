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

# Use existing Bicep template
echo -e "${YELLOW}Step 3: Using existing Bicep template...${NC}"
# We're using the main.bicep file that's already in this directory

# Validate deployment first
echo -e "${YELLOW}Step 4a: Validating Azure deployment...${NC}"
az deployment group what-if --resource-group rg-rhinomcp-remote-dev --template-file main.bicep

# Ask for confirmation
echo -e "${YELLOW}Review the deployment changes above. Do you want to proceed? (y/n)${NC}"
read -r confirmation
if [[ $confirmation != "y" && $confirmation != "Y" ]]; then
    echo -e "${RED}Deployment cancelled.${NC}"
    exit 1
fi

# Deploy resources
echo -e "${YELLOW}Step 4b: Deploying Azure resources (this may take a few minutes)...${NC}"
az deployment group create --resource-group rg-rhinomcp-remote-dev --template-file main.bicep

# Get deployment outputs
echo -e "${YELLOW}Step 5: Getting deployment outputs...${NC}"
webAppUrl=$(az deployment group show --resource-group rg-rhinomcp-remote-dev --name main --query properties.outputs.webAppUrl.value -o tsv)
signalRUrl=$(az deployment group show --resource-group rg-rhinomcp-remote-dev --name main --query properties.outputs.signalRUrl.value -o tsv)
baseName=$(az deployment group show --resource-group rg-rhinomcp-remote-dev --name main --query properties.parameters.baseName.value -o tsv)

# If baseName is empty, use default
if [ -z "$baseName" ]; then
    baseName="rhinomcp"
    echo -e "Using default base name: ${baseName}"
fi

# Create minimal requirements file
echo -e "${YELLOW}Step 6: Creating minimal requirements file...${NC}"
cat > minimal-requirements.txt << 'REQUIREMENTS'
azure-signalr==1.2.0
azure-functions==1.13.3
azure-data-tables==12.4.0
azure-storage-blob==12.14.1
azure-core==1.25.0
aiohttp==3.8.4
requests==2.28.2
fastapi==0.95.0
uvicorn==0.21.1
python-dotenv==1.0.0
REQUIREMENTS

# Create deployment package
echo -e "${YELLOW}Step 7: Creating deployment package...${NC}"
cd rhino_mcp_server
cp ../minimal-requirements.txt requirements.txt
zip -r ../deployment.zip azure_server.py requirements.txt static/codes.html azure-requirements.txt
cd ..

# Deploy to App Service
echo -e "${YELLOW}Step 8: Deploying server code to App Service...${NC}"
az webapp deployment source config-zip --resource-group rg-rhinomcp-remote-dev --name "${baseName}-app" --src deployment.zip

# Set startup command
echo -e "${YELLOW}Step 9: Setting startup command...${NC}"
az webapp config set --resource-group rg-rhinomcp-remote-dev --name "${baseName}-app" --startup-file "python azure_server.py"

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
