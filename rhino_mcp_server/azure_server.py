import os
import argparse
import asyncio
import json
import logging
import aiohttp
import requests
import datetime
from urllib.parse import urlparse
from rhinomcp.server import mcp, RhinoConnection
from azure.signalr.aio import (
    SignalRServiceClient,
    AuthType,
    TokenType
)
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from typing import Dict, Any, List, Optional
from azure.data.tables import TableServiceClient, TableClient
import os.path

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger("RhinoMCPAzureServer")

# Global SignalR client
signalr_client = None
# Connected clients tracking
connected_clients = set()
# Connection registry API URL
CONNECTION_REGISTRY_URL = os.environ.get("CONNECTION_REGISTRY_URL", "https://rhinomcp-app.azurewebsites.net/api/")
# Current connection code
current_connection_code = None
# Azure Storage connection string
STORAGE_CONNECTION_STRING = os.environ.get("AZURE_STORAGE_CONNECTION_STRING")

# Create FastAPI app
app = FastAPI()

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Serve static files (for the codes.html page)
static_dir = os.path.join(os.path.dirname(__file__), "static")
app.mount("/static", StaticFiles(directory=static_dir), name="static")

class AzureRhinoConnection:
    """Class to handle communication with Rhino via Azure SignalR"""
    
    def __init__(self, signalr_client):
        self.signalr_client = signalr_client
        self.hub_name = "rhinomcp"
        self.client_id = None
        
    async def connect(self):
        """Wait for a Rhino client to connect"""
        # The connection is handled by SignalR
        # We'll just log that we're waiting
        logger.info("Waiting for Rhino client to connect via Azure SignalR...")
        return True
        
    async def disconnect(self):
        """Disconnect from SignalR"""
        # SignalR handles the connection lifecycle
        pass
        
    async def send_command(self, function_name, arguments):
        """Send a command to Rhino via SignalR"""
        if not connected_clients:
            raise Exception("No Rhino clients connected")
            
        # Construct the message
        message = {
            "function": function_name,
            "arguments": arguments
        }
        
        message_json = json.dumps(message)
        
        # Send to all connected clients (typically just one)
        for client_id in connected_clients:
            # Send the message through SignalR
            await self.signalr_client.send_to_user(self.hub_name, client_id, "ReceiveCommand", [message_json])
            
        # Wait for response (implemented in the SignalR message handler)
        # This is handled by the on_client_message callback
        
    async def handle_client_message(self, client_id, message):
        """Process a message from a Rhino client"""
        # This will be called by the SignalR message handler
        logger.info(f"Received message from client {client_id}")
        # Process the response based on your requirements

async def on_client_connected(client_id):
    """Handle client connection"""
    logger.info(f"Rhino client connected: {client_id}")
    connected_clients.add(client_id)
    
async def on_client_disconnected(client_id):
    """Handle client disconnection"""
    logger.info(f"Rhino client disconnected: {client_id}")
    if client_id in connected_clients:
        connected_clients.remove(client_id)
    
async def on_client_message(client_id, message):
    """Handle a message from a client"""
    logger.info(f"Message from client {client_id}: {message}")
    # Process the message based on your requirements
    # You would typically parse the JSON and update your application state

async def generate_connection_code(signalr_url):
    """Generate a user-friendly connection code"""
    global current_connection_code
    
    try:
        # Call the connection registry API
        async with aiohttp.ClientSession() as session:
            response = await session.post(
                f"{CONNECTION_REGISTRY_URL}GenerateConnectionCode",
                json={"signalRUrl": signalr_url}
            )
            
            if response.status != 200:
                logger.error(f"Failed to generate connection code: {response.status}")
                return None
                
            data = await response.json()
            connection_code = data.get("code")
            
            if connection_code:
                current_connection_code = connection_code
                logger.info(f"Generated connection code: {connection_code}")
                return connection_code
                
    except Exception as e:
        logger.error(f"Error generating connection code: {e}")
        
    return None

# API endpoint to get active connection codes
@app.get("/api/codes")
async def get_connection_codes():
    """Get all active connection codes"""
    if not STORAGE_CONNECTION_STRING:
        return {"codes": []}
    
    try:
        # Connect to the Azure Table Storage
        table_service = TableServiceClient.from_connection_string(STORAGE_CONNECTION_STRING)
        table_client = table_service.get_table_client("ConnectionCodes")
        
        # Query for all connection codes in the last 24 hours
        now = datetime.datetime.utcnow()
        yesterday = now - datetime.timedelta(days=1)
        
        codes = []
        query_filter = f"PartitionKey eq 'RhinoMCP' and LastAccessTime gt datetime'{yesterday.isoformat()}Z'"
        
        entities = table_client.query_entities(query_filter)
        
        for entity in entities:
            codes.append({
                "code": entity.get("Code"),
                "createdTime": entity.get("CreatedTime"),
                "lastAccessTime": entity.get("LastAccessTime"),
                "connectionCount": entity.get("ConnectionCount", 0)
            })
        
        return {"codes": codes}
    except Exception as e:
        logger.error(f"Error retrieving connection codes: {e}")
        return {"codes": [], "error": str(e)}

# Web page to display connection codes
@app.get("/codes", response_class=HTMLResponse)
async def codes_page():
    """Serve the connection codes web page"""
    try:
        with open(os.path.join(static_dir, "codes.html"), "r") as f:
            return f.read()
    except Exception as e:
        logger.error(f"Error serving codes page: {e}")
        return f"<html><body><h1>Error</h1><p>{str(e)}</p></body></html>"

async def init_signalr(connection_string, hub_name="rhinomcp"):
    """Initialize the SignalR client"""
    try:
        # Create the SignalR client
        client = SignalRServiceClient(connection_string)
        
        # Set up event handlers
        client.on_client_event("register", on_client_connected)
        client.on_client_disconnected(on_client_disconnected)
        client.on_event("SendResponse", on_client_message)
        
        # Start the client
        await client.start()
        
        logger.info(f"SignalR client started for hub: {hub_name}")
        return client
    except Exception as e:
        logger.error(f"Failed to initialize SignalR client: {e}")
        raise

def main():
    """Entry point for the Azure-enabled RhinoMCP server"""
    parser = argparse.ArgumentParser(description='Run RhinoMCP server with Azure support')
    parser.add_argument('--azure-connection-string', type=str, default=os.environ.get('AZURE_SIGNALR_CONNECTION_STRING'),
                        help='Azure SignalR connection string')
    parser.add_argument('--local', action='store_true',
                        help='Run in local mode (TCP) instead of Azure mode')
    parser.add_argument('--host', type=str, default="127.0.0.1",
                        help='Remote Rhino host IP address for local mode (default: 127.0.0.1)')
    parser.add_argument('--port', type=int, default=1999,
                        help='Remote Rhino port for local mode (default: 1999)')
    
    # Parse arguments
    args = parser.parse_args()
    
    if args.local:
        # Run in local (TCP) mode
        from rhinomcp.server import main as server_main
        from rhinomcp.server import get_rhino_connection
        
        # Initialize connection with provided host/port
        try:
            get_rhino_connection(host=args.host, port=args.port)
            logger.info(f"Connected to Rhino at {args.host}:{args.port}")
        except Exception as e:
            logger.warning(f"Warning: {str(e)}")
            logger.info("Will try to connect when needed.")
        
        # Run the server
        server_main()
    else:
        # Run in Azure mode
        if not args.azure_connection_string:
            logger.error("Azure SignalR connection string required. Set AZURE_SIGNALR_CONNECTION_STRING or use --azure-connection-string")
            return
            
        try:
            # Create an async event loop
            loop = asyncio.get_event_loop()
            
            # Initialize Azure SignalR
            global signalr_client
            signalr_client = loop.run_until_complete(init_signalr(args.azure_connection_string))
            
            # Create the Rhino connection
            rhino = AzureRhinoConnection(signalr_client)
            
            # Run the server
            # The main server should be modified to use the AzureRhinoConnection
            # For now, we'll just keep the event loop running
            logger.info("RhinoMCP Azure server running. Press Ctrl+C to exit.")
            loop.run_forever()
        except KeyboardInterrupt:
            logger.info("Server stopping...")
        except Exception as e:
            logger.error(f"Server error: {e}")
        finally:
            # Clean up
            if signalr_client:
                loop.run_until_complete(signalr_client.stop())
            loop.close()
            logger.info("Server stopped")

if __name__ == "__main__":
    main()
