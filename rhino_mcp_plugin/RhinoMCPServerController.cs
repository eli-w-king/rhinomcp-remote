using System;
using System.Threading.Tasks;
using Rhino;

namespace RhinoMCPPlugin
{
    public static class RhinoMCPServerController
    {
        private static readonly object syncLock = new object();
        private static RhinoMCPServer localServer;
        private static RhinoMCPAzureClient azureClient;
        private static bool isRunning;

        // Enum to track connection type
        public enum ConnectionMode
        {
            Local,
            Azure
        }

        // Current mode of operation
        public static ConnectionMode CurrentMode { get; private set; } = ConnectionMode.Local;

        static RhinoMCPServerController()
        {
            localServer = null;
            azureClient = null;
            isRunning = false;
        }

        // Start the local TCP server
        public static void StartServer(string host = "127.0.0.1", int port = 1999)
        {
            lock (syncLock)
            {
                if (isRunning)
                {
                    if (CurrentMode == ConnectionMode.Local)
                    {
                        RhinoApp.WriteLine("Local MCP server is already running");
                    }
                    else
                    {
                        RhinoApp.WriteLine("Azure MCP client is running. Please stop it before starting local server.");
                    }
                    return;
                }

                try
                {
                    // Create and start the server
                    localServer = new RhinoMCPServer(host, port);
                    localServer.Start();
                    isRunning = true;
                    CurrentMode = ConnectionMode.Local;
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error starting local MCP server: {ex.Message}");
                    localServer = null;
                }
            }
        }

        // Connect to Azure SignalR service
        public static async Task ConnectToAzureAsync(string signalRUrl, string apiKey = null)
        {
            lock (syncLock)
            {
                if (isRunning)
                {
                    if (CurrentMode == ConnectionMode.Azure)
                    {
                        RhinoApp.WriteLine("Already connected to Azure MCP service");
                    }
                    else
                    {
                        RhinoApp.WriteLine("Local MCP server is running. Please stop it before connecting to Azure.");
                    }
                    return;
                }
            }

            try
            {
                // Create and connect the Azure client
                azureClient = new RhinoMCPAzureClient();
                bool success = await azureClient.ConnectAsync(signalRUrl, apiKey);

                lock (syncLock)
                {
                    if (success)
                    {
                        isRunning = true;
                        CurrentMode = ConnectionMode.Azure;
                        RhinoApp.WriteLine("Connected to Azure MCP service");
                    }
                    else
                    {
                        azureClient = null;
                        RhinoApp.WriteLine("Failed to connect to Azure MCP service");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error connecting to Azure MCP service: {ex.Message}");
                azureClient = null;
            }
        }

        // Stop the server or disconnect from Azure
        public static async Task StopAsync()
        {
            lock (syncLock)
            {
                if (!isRunning)
                {
                    RhinoApp.WriteLine("MCP is not running");
                    return;
                }
            }

            try
            {
                if (CurrentMode == ConnectionMode.Local && localServer != null)
                {
                    localServer.Stop();
                    localServer = null;
                }
                else if (CurrentMode == ConnectionMode.Azure && azureClient != null)
                {
                    await azureClient.DisconnectAsync();
                    azureClient = null;
                }

                lock (syncLock)
                {
                    isRunning = false;
                    RhinoApp.WriteLine("MCP stopped");
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error stopping MCP: {ex.Message}");
            }
        }

        // Check if server is running
        public static bool IsRunning()
        {
            lock (syncLock)
            {
                return isRunning;
            }
        }
        
        // Legacy methods for backward compatibility
        public static void StopServer()
        {
            Task.Run(async () => await StopAsync()).Wait();
        }
        
        public static bool IsServerRunning()
        {
            return IsRunning();
        }
    }
}
