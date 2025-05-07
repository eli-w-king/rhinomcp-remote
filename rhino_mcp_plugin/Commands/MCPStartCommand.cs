using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Net;

namespace RhinoMCPPlugin.Commands
{
    public class MCPStartCommand : Command
    {
        public MCPStartCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static MCPStartCommand Instance { get; private set; }

        

        public override string EnglishName => "mcpstart";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Default values
            string host = "127.0.0.1";
            int port = 1999;
            
            // Get remote server address from user
            Result res = RhinoGet.GetString("Enter remote MCP server IP address (or press Enter for localhost)", true, ref host);
            if (res != Result.Success)
                return res;
                
            if (string.IsNullOrWhiteSpace(host))
                host = "127.0.0.1";
                
            // Validate IP address format
            if (host != "127.0.0.1" && host != "localhost")
            {
                try {
                    IPAddress.Parse(host);
                }
                catch (Exception) {
                    RhinoApp.WriteLine("Invalid IP address format. Please use format like '192.168.1.100'");
                    return Result.Failure;
                }
            }
            
            // Get port from user
            string portStr = "1999";
            res = RhinoGet.GetString("Enter remote MCP server port (or press Enter for default 1999)", true, ref portStr);
            if (res != Result.Success)
                return res;
                
            if (!string.IsNullOrWhiteSpace(portStr))
            {
                if (!int.TryParse(portStr, out port))
                {
                    RhinoApp.WriteLine("Invalid port number. Using default port 1999");
                    port = 1999;
                }
            }
            
            RhinoApp.WriteLine($"Connecting to MCP server at {host}:{port}");
            RhinoMCPServerController.StartServer(host, port);
            return Result.Success;
        }

    }
}
