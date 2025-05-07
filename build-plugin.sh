#!/bin/bash

# Terminal colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Building RhinoMCP Plugin...${NC}"

# Navigate to the plugin directory
cd "$(dirname "$0")/rhino_mcp_plugin"

# Clean previous builds
echo -e "${YELLOW}Cleaning previous builds...${NC}"
dotnet clean -c Release

# Restore packages
echo -e "${YELLOW}Restoring NuGet packages...${NC}"
dotnet restore

# Build the project
echo -e "${YELLOW}Building the project...${NC}"
dotnet build -c Release

# Check if build succeeded
if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed!${NC}"
    exit 1
fi

# Get output directory
OUTPUT_DIR="$(pwd)/bin/Release/net7.0"

echo -e "${GREEN}Build completed successfully!${NC}"
echo -e "Plugin located at: $OUTPUT_DIR/rhinomcp.rhp"

# Create a distribution package
echo -e "${YELLOW}Creating distribution package...${NC}"
DIST_DIR="$(dirname "$(pwd)")/rhinomcp-dist"
mkdir -p "$DIST_DIR"

# Copy the plugin
cp "$OUTPUT_DIR/rhinomcp.rhp" "$DIST_DIR/"

# Copy documentation and additional files
cp "$(dirname "$(pwd)")/README.md" "$DIST_DIR/"
cp "$(dirname "$(pwd)")/LICENSE" "$DIST_DIR/"

# Files are now maintained directly in the distribution directory
# No need to copy from root

# Copy assets directory
cp -r "$(dirname "$(pwd)")/assets" "$DIST_DIR/"

# Create minimal server package
mkdir -p "$DIST_DIR/rhino_mcp_server"
mkdir -p "$DIST_DIR/rhino_mcp_server/static"
cp "$(dirname "$(pwd)")/rhino_mcp_server/azure_server.py" "$DIST_DIR/rhino_mcp_server/"
cp "$(dirname "$(pwd)")/rhino_mcp_server/azure-requirements.txt" "$DIST_DIR/rhino_mcp_server/"
cp "$(dirname "$(pwd)")/rhino_mcp_server/static/codes.html" "$DIST_DIR/rhino_mcp_server/static/"

# Create installation guide
cat > "$DIST_DIR/INSTALL.md" << 'EOF'
# RhinoMCP Plugin Installation

## For Rhino 8 on Windows:
1. Copy `rhinomcp.rhp` to `%APPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\`
2. Or use the Rhino Plugin Manager:
   - Type `PlugInManager` in the Rhino command line
   - Click "Install..." and browse to the .rhp file

## For Rhino 8 on macOS:
1. Copy `rhinomcp.rhp` to `/Users/[YourUsername]/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/`
2. Or use the Rhino Plugin Manager:
   - Type `PlugInManager` in the Rhino command line
   - Click "Install..." and browse to the .rhp file

## Using the plugin:
- Type `mcpstart` to start the MCP server
- Type `mcpconnectazure` to connect to Azure
- Type `mcpstop` to stop the server
- Type `mcpversion` to see the plugin version

## Azure Setup:
See `azure-setup.md` for instructions on setting up Azure resources.
EOF

echo -e "${GREEN}Distribution package created at: $DIST_DIR${NC}"
