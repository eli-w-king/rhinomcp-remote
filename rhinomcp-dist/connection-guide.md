# Finding Your Connection Code in Azure

To view your connection codes in the Azure Portal:

1. **Open the Azure Portal**
   - Navigate to https://portal.azure.com

2. **Go to Your Storage Account**
   - Find the storage account created with your RhinoMCP resources
   - It will be named something like `rhinomcpstorage` (without hyphens)

3. **Open Table Service**
   - In the left menu, under "Data storage", click on "Tables"
   - Find and click on the "ConnectionCodes" table

4. **View Connection Codes**
   - Here you'll see all active connection codes
   - Each entry contains:
     - The connection code (e.g., "RedRhino123") 
     - The SignalR URL it's linked to
     - Creation time
     - Last access time
     - Connection count

5. **For a Web Interface**
   - Visit your Azure App Service URL (from deployment outputs)
   - Add "/codes" to the URL (e.g., https://rhinomcp-app.azurewebsites.net/codes)
   - This displays a simple web page showing current active codes

Connection codes will automatically expire after 24 hours of inactivity.

## Creating a New Connection Code

New connection codes are automatically generated when:

1. Your MCP server starts up in Azure
2. You manually request a new code via the API:
   ```
   POST https://rhinomcp-app.azurewebsites.net/api/generate-code
   ```

Each MCP server instance can have multiple active codes, allowing different groups of users to connect.
