# MCP Server (.NET Core)

This project is a .NET Core Web API implementation of an MCP (Model Context Protocol) server, inspired by the Node.js reference. It provides:

- Diagnostics and status endpoints
- Tool listing and execution endpoints
- Query results listing and retrieval
- Server-Sent Events (SSE) support for real-time communication
- SQL Server database connectivity
- Security middleware (rate limiting, CORS, etc.)
- Extensible architecture for registering tools and resources

## Getting Started

1. Ensure you have .NET 8.0 SDK or later installed.
2. Restore dependencies:
   ```bash
   dotnet restore
   ```
3. Build the project:
   ```bash
   dotnet build
   ```
4. Run the project:
   ```bash
   dotnet run
   ```

## Configuration
- Database connection and other settings will be managed via `appsettings.json` and environment variables.

## Next Steps
- Implement endpoints and features as described in the Node.js reference.
- See `.github/copilot-instructions.md` for Copilot guidance.
