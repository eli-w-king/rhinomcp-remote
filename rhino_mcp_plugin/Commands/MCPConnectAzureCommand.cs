using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Input;

namespace RhinoMCPPlugin.Commands
{
    public class MCPConnectAzureCommand : Command
    {
        public MCPConnectAzureCommand()
        {
            Instance = this;
        }

        public static MCPConnectAzureCommand Instance { get; private set; }

        public override string EnglishName => "mcpconnectazure";
        
        // Base URL for the connection registry API
        private const string ConnectionRegistryUrl = "https://rhinomcp-app.azurewebsites.net/api/";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Ask the user if they want to connect via code or direct URL
            var options = new string[] { "Connection Code", "Direct URL" };
            int option = -1;
            var res = RhinoGet.GetInteger("Connection method (0=Code, 1=URL)", false, ref option, 0, 1);
            if (res != Result.Success)
                return res;
                
            if (option == 0)
            {
                // Connection via code
                return ConnectViaCode();
            }
            else
            {
                // Direct URL connection
                return ConnectViaDirectUrl();
            }
        }
        
        private Result ConnectViaCode()
        {
            // Get connection code from user
            string connectionCode = "";
            var res = RhinoGet.GetString("Enter connection code (e.g., RedRhino123)", false, ref connectionCode);
            if (res != Result.Success)
                return res;
                
            if (string.IsNullOrWhiteSpace(connectionCode))
            {
                RhinoApp.WriteLine("Connection code is required");
                return Result.Failure;
            }
            
            // Resolve the code to get the SignalR URL
            try
            {
                RhinoApp.WriteLine("Resolving connection code...");
                
                using (var httpClient = new HttpClient())
                {
                    Task<HttpResponseMessage> task = Task.Run(() => httpClient.GetAsync(ConnectionRegistryUrl + "resolve/" + connectionCode));
                    task.Wait();
                    
                    var response = task.Result;
                    if (response.IsSuccessStatusCode)
                    {
                        Task<string> readTask = Task.Run(() => response.Content.ReadAsStringAsync());
                        readTask.Wait();
                        
                        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(readTask.Result);
                        string signalRUrl = jsonResponse.GetProperty("signalRUrl").GetString();
                        
                        // Connect to Azure SignalR with the resolved URL
                        Task connectTask = Task.Run(async () =>
                        {
                            RhinoApp.WriteLine($"Connecting to MCP server...");
                            await RhinoMCPServerController.ConnectToAzureAsync(signalRUrl);
                        });
                        connectTask.Wait();
                        
                        return Result.Success;
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Failed to resolve connection code: {response.ReasonPhrase}");
                        return Result.Failure;
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error connecting via code: {ex.Message}");
                return Result.Failure;
            }
        }
        
        private Result ConnectViaDirectUrl()
        {
            // Default values - you would typically store this in settings
            string serviceUrl = "";
            string apiKey = "";
            
            // Get the Azure SignalR service URL
            Result res = RhinoGet.GetString("Enter Azure SignalR service URL", false, ref serviceUrl);
            if (res != Result.Success)
                return res;
                
            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                RhinoApp.WriteLine("Azure service URL is required");
                return Result.Failure;
            }
            
            // Validate URL format
            if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out _))
            {
                RhinoApp.WriteLine("Invalid URL format. Please use format like 'https://your-service.service.signalr.net'");
                return Result.Failure;
            }
            
            // Get API key (optional)
            res = RhinoGet.GetString("Enter Azure API key (optional)", true, ref apiKey);
            if (res != Result.Success)
                return res;
            
            // Connect to Azure
            try
            {
                Task.Run(async () =>
                {
                    RhinoApp.WriteLine($"Connecting to Azure MCP service at {serviceUrl}...");
                    await RhinoMCPServerController.ConnectToAzureAsync(serviceUrl, 
                        string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
                }).Wait();
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Failed to connect to Azure: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}
