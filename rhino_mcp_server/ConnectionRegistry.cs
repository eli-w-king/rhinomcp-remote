using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using System.Security.Cryptography;

namespace RhinoMCPAzure.API
{
    // Entity for storing connection information in Azure Table Storage
    public class ConnectionEntity : TableEntity
    {
        public string Code { get; set; }
        public string SignalRUrl { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public int ConnectionCount { get; set; }
        public string CreatorId { get; set; }
        
        public ConnectionEntity() { }
        
        public ConnectionEntity(string code, string signalRUrl, string creatorId)
        {
            PartitionKey = "RhinoMCP";
            RowKey = code;
            Code = code;
            SignalRUrl = signalRUrl;
            CreatedTime = DateTime.UtcNow;
            LastAccessTime = DateTime.UtcNow;
            ConnectionCount = 0;
            CreatorId = creatorId;
        }
    }

    public static class ConnectionRegistry
    {
        private static readonly string[] Adjectives = { "Red", "Blue", "Green", "Happy", "Swift", "Clever", "Bright", "Bold" };
        private static readonly string[] Animals = { "Rhino", "Tiger", "Eagle", "Wolf", "Panda", "Lion", "Falcon", "Bear" };
        private static readonly Random random = new Random();
        
        // Azure Function to generate a new connection code
        [FunctionName("GenerateConnectionCode")]
        public static async Task<IActionResult> GenerateCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [Table("ConnectionCodes", Connection = "AzureWebJobsStorage")] CloudTable connectionTable,
            ILogger log)
        {
            log.LogInformation("Generating new connection code");
            
            // Read request body for SignalR URL
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
            
            // Validate input
            if (data == null || !data.ContainsKey("signalRUrl"))
            {
                return new BadRequestObjectResult("Please provide a signalRUrl in the request body");
            }
            
            string signalRUrl = data["signalRUrl"];
            string userId = data.ContainsKey("userId") ? data["userId"] : "anonymous";
            
            // Generate a unique, human-readable code
            string code = GenerateUniqueCode();
            
            // Store in Azure Table Storage
            var connectionEntity = new ConnectionEntity(code, signalRUrl, userId);
            
            // Create the table if it doesn't exist
            await connectionTable.CreateIfNotExistsAsync();
            
            // Insert the entity
            TableOperation insertOperation = TableOperation.Insert(connectionEntity);
            await connectionTable.ExecuteAsync(insertOperation);
            
            // Return the code
            return new OkObjectResult(new { code = code });
        }
        
        // Azure Function to resolve a connection code to a SignalR URL
        [FunctionName("ResolveConnectionCode")]
        public static async Task<IActionResult> ResolveCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "resolve/{code}")] HttpRequest req,
            string code,
            [Table("ConnectionCodes", Connection = "AzureWebJobsStorage")] CloudTable connectionTable,
            ILogger log)
        {
            log.LogInformation($"Resolving connection code: {code}");
            
            if (string.IsNullOrEmpty(code))
            {
                return new BadRequestObjectResult("Please provide a connection code");
            }
            
            // Look up the code
            TableOperation retrieveOperation = TableOperation.Retrieve<ConnectionEntity>("RhinoMCP", code);
            TableResult result = await connectionTable.ExecuteAsync(retrieveOperation);
            
            if (result.Result == null)
            {
                return new NotFoundObjectResult("Connection code not found");
            }
            
            var entity = (ConnectionEntity)result.Result;
            
            // Update last access time and connection count
            entity.LastAccessTime = DateTime.UtcNow;
            entity.ConnectionCount++;
            
            TableOperation updateOperation = TableOperation.Replace(entity);
            await connectionTable.ExecuteAsync(updateOperation);
            
            // Return the SignalR URL
            return new OkObjectResult(new { signalRUrl = entity.SignalRUrl });
        }
        
        // Helper method to generate a unique, human-readable code
        private static string GenerateUniqueCode()
        {
            string adjective = Adjectives[random.Next(Adjectives.Length)];
            string animal = Animals[random.Next(Animals.Length)];
            
            // Add a random 3-digit number for uniqueness
            int number = random.Next(100, 1000);
            
            return $"{adjective}{animal}{number}";
        }
    }
}
