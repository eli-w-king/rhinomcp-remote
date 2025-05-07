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
