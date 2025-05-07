using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using RhinoMCPPlugin.Functions;

namespace RhinoMCPPlugin
{
    public class RhinoMCPAzureClient
    {
        private string serviceUrl;
        private HubConnection hubConnection;
        private bool isConnected;
        private readonly object lockObject = new object();
        private RhinoMCPFunctions handler;

        public RhinoMCPAzureClient()
        {
            this.serviceUrl = null;
            this.hubConnection = null;
            this.isConnected = false;
            this.handler = new RhinoMCPFunctions();
        }

        public async Task<bool> ConnectAsync(string signalRUrl, string apiKey = null)
        {
            lock (lockObject)
            {
                if (isConnected)
                {
                    RhinoApp.WriteLine("Already connected to Azure SignalR");
                    return true;
                }

                this.serviceUrl = signalRUrl;
            }

            try
            {
                // Build the SignalR connection with optional API key authentication
                var builder = new HubConnectionBuilder()
                    .WithUrl(signalRUrl, options =>
                    {
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            options.Headers.Add("x-api-key", apiKey);
                        }
                        
                        // Configure other options like retry policy, etc.
                        options.HttpMessageHandlerFactory = (handler) =>
                        {
                            if (handler is HttpClientHandler clientHandler)
                            {
                                // Bypass certificate validation in development
                                // NOTE: Do NOT do this in production
                                #if DEBUG
                                clientHandler.ServerCertificateCustomValidationCallback = 
                                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                #endif
                            }
                            return handler;
                        };
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                    .Build();

                // Register message handler
                hubConnection.On<string>("ReceiveCommand", async (message) =>
                {
                    try
                    {
                        // Parse the JSON message
                        JObject jsonMessage = JObject.Parse(message);
                        
                        // Process the message with our handler
                        string functionName = jsonMessage["function"]?.ToString();
                        JToken arguments = jsonMessage["arguments"];

                        if (string.IsNullOrEmpty(functionName) || arguments == null)
                        {
                            await SendResponseAsync(new { error = "Invalid message format" });
                            return;
                        }

                        // Execute the function
                        JObject result = handler.ExecuteFunction(functionName, arguments);
                        
                        // Send response back through SignalR
                        await SendResponseAsync(result);
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error handling message: {ex.Message}");
                        await SendResponseAsync(new { error = ex.Message });
                    }
                });

                // Set up reconnection and disconnection handlers
                hubConnection.Reconnecting += (error) =>
                {
                    RhinoApp.WriteLine($"Connection lost. Attempting to reconnect... Error: {error?.Message}");
                    return Task.CompletedTask;
                };

                hubConnection.Reconnected += (connectionId) =>
                {
                    RhinoApp.WriteLine($"Reconnected with connection ID: {connectionId}");
                    return Task.CompletedTask;
                };

                hubConnection.Closed += (error) =>
                {
                    isConnected = false;
                    RhinoApp.WriteLine($"Connection closed. Error: {error?.Message}");
                    return Task.CompletedTask;
                };

                // Start the connection
                await hubConnection.StartAsync();
                
                // Register this client with the server
                await hubConnection.InvokeAsync("Register", "RhinoClient");

                lock (lockObject)
                {
                    isConnected = true;
                }

                RhinoApp.WriteLine($"Connected to RhinoMCP Azure service at {signalRUrl}");
                return true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Failed to connect to Azure SignalR: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            lock (lockObject)
            {
                if (!isConnected)
                {
                    return;
                }

                isConnected = false;
            }

            if (hubConnection != null)
            {
                try
                {
                    await hubConnection.StopAsync();
                    await hubConnection.DisposeAsync();
                    hubConnection = null;
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error during disconnection: {ex.Message}");
                }
            }

            RhinoApp.WriteLine("Disconnected from Azure RhinoMCP service");
        }

        private async Task SendResponseAsync(object response)
        {
            if (hubConnection?.State == HubConnectionState.Connected)
            {
                try
                {
                    string jsonResponse = JsonConvert.SerializeObject(response);
                    await hubConnection.InvokeAsync("SendResponse", "RhinoClient", jsonResponse);
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Failed to send response: {ex.Message}");
                }
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (lockObject)
                {
                    return isConnected && hubConnection?.State == HubConnectionState.Connected;
                }
            }
        }
    }
}
