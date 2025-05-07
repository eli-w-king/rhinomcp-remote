import argparse
from rhinomcp.server import main as server_main
from rhinomcp.server import mcp, get_rhino_connection

def main():
    """Entry point for the rhinomcp package with command line arguments for remote connection"""
    parser = argparse.ArgumentParser(description='Run RhinoMCP server with remote connection support')
    parser.add_argument('--host', type=str, default="127.0.0.1", 
                        help='Remote Rhino host IP address (default: 127.0.0.1)')
    parser.add_argument('--port', type=int, default=1999, 
                        help='Remote Rhino port (default: 1999)')
    
    # Parse arguments
    args = parser.parse_args()
    
    # Initialize connection with provided host/port
    try:
        get_rhino_connection(host=args.host, port=args.port)
        print(f"Connected to Rhino at {args.host}:{args.port}")
    except Exception as e:
        print(f"Warning: {str(e)}")
        print("Will try to connect when needed.")
    
    # Run the server
    server_main()

if __name__ == "__main__":
    main()