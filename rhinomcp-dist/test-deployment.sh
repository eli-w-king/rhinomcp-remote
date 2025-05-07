#!/bin/bash

# Terminal colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${YELLOW}RhinoMCP Azure Deployment Test Script${NC}"
echo "----------------------------------------------------------------"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}Azure CLI not found. Please install it first:${NC}"
    echo "brew install azure-cli"
    exit 1
fi

# Check login status
echo -e "${YELLOW}Step 1: Checking Azure login status...${NC}"
account=$(az account show --query name -o tsv 2>/dev/null)
if [ $? -ne 0 ]; then
    echo -e "${RED}Not logged in to Azure. Please login first:${NC}"
    echo "az login"
    exit 1
else
    echo -e "${GREEN}Logged in as: ${account}${NC}"
fi

# Check if resource group exists
echo -e "${YELLOW}Step 2: Checking if resource group exists...${NC}"
rgExists=$(az group exists --name rg-rhinomcp-remote-dev)
if [ "$rgExists" == "true" ]; then
    echo -e "${GREEN}Resource group 'rg-rhinomcp-remote-dev' exists${NC}"
else
    echo -e "${RED}Resource group 'rg-rhinomcp-remote-dev' does not exist${NC}"
    echo "Please create the resource group first:"
    echo "az group create --name rg-rhinomcp-remote-dev --location <location>"
    exit 1
fi

# Validate Bicep template
echo -e "${YELLOW}Step 3: Validating Bicep template...${NC}"
if [ -f "./main.bicep" ]; then
    az bicep build --file ./main.bicep
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Bicep template validation successful${NC}"
    else
        echo -e "${RED}Bicep template validation failed${NC}"
        exit 1
    fi
else
    echo -e "${RED}Bicep template file not found${NC}"
    exit 1
fi

# Check if python files exist
echo -e "${YELLOW}Step 4: Checking Python server files...${NC}"
if [ -f "./rhino_mcp_server/azure_server.py" ] && [ -f "./rhino_mcp_server/azure-requirements.txt" ]; then
    echo -e "${GREEN}Server files exist${NC}"
else
    echo -e "${RED}Server files missing${NC}"
    exit 1
fi

# Check if the plugin binary exists
echo -e "${YELLOW}Step 5: Checking plugin binary...${NC}"
if [ -f "./rhinomcp.rhp" ]; then
    echo -e "${GREEN}Plugin binary exists${NC}"
else
    echo -e "${RED}Plugin binary missing${NC}"
    echo "Make sure to build the plugin first using build-plugin.sh"
    exit 1
fi

# Test zip creation
echo -e "${YELLOW}Step 6: Testing deployment package creation...${NC}"
cd rhino_mcp_server
zip -r ../test-deployment.zip azure_server.py azure-requirements.txt static/codes.html >/dev/null
if [ $? -eq 0 ]; then
    echo -e "${GREEN}Deployment package creation successful${NC}"
    rm ../test-deployment.zip
else
    echo -e "${RED}Deployment package creation failed${NC}"
    exit 1
fi
cd ..

# Run what-if deployment to check for potential issues
echo -e "${YELLOW}Step 7: Running deployment validation (what-if)...${NC}"
echo -e "${YELLOW}This will check for potential issues without actually deploying.${NC}"
az deployment group what-if --resource-group rg-rhinomcp-remote-dev --template-file main.bicep --no-pretty-print --query changes[].changeType -o tsv

# All tests passed
echo -e "\n${GREEN}All tests passed! Your deployment files are ready.${NC}"
echo -e "To deploy to Azure, run:"
echo -e "  ./deploy.sh"
echo -e "\nEnjoy using RhinoMCP Remote!"
