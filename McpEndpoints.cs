using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Text;


using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using DOTNETCONVERTED;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        // Diagnostics endpoint
        app.MapGet("/diagnostic", ([FromServices] IConfiguration config) =>
        {
            var dbInfo = config.GetSection("Database").Get<DbConfig>() ?? new DbConfig();
            var info = new
            {
                status = "ok",
                message = "MCP Server is running",
                transport = "http/sse",
                endpoints = new
                {
                    sse = "/sse",
                    messages = "/messages",
                    diagnostics = "/diagnostic",
                    query_results = new { list = "/query-results", detail = "/query-results/{uuid}" }
                },
                database_info = dbInfo,
                version = "1.0.0-dotnet"
            };
            return Results.Json(info);
        });


        // Tool listing endpoint
        app.MapGet("/tools", () =>
        {
            var tools = DOTNETCONVERTED.ToolRegistry.List()
                .Select(t => new {
                    name = t.Name,
                    schema = t.Schema,
                    source = t.Source
                }).ToList();
            return Results.Json(new { count = tools.Count, tools });
        });

        // Tool call endpoint (AI/agent friendly)
        app.MapPost("/tools/call", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            var json = System.Text.Json.JsonDocument.Parse(body).RootElement;
            var toolName = json.GetProperty("name").GetString();
            var args = json.TryGetProperty("arguments", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Object
                ? JsonNode.Parse(a.GetRawText()) as JsonObject ?? new JsonObject()
                : new JsonObject();

            var tool = DOTNETCONVERTED.ToolRegistry.Get(toolName ?? "");
            if (tool == null)
                return Results.NotFound(new { error = $"Tool not found: {toolName}" });

            try
            {
                var result = await tool.Handler(args);
                return Results.Json(new { result });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message });
            }
        });

        // Query results listing
        app.MapGet("/query-results", () =>
        {
            var results = QueryResultStore.ListResults();
            return Results.Json(new { results });
        });

        // Query result detail
        app.MapGet("/query-results/{uuid}", (string uuid) =>
        {
            var result = QueryResultStore.GetResult(uuid);
            if (result == null) return Results.NotFound(new { error = $"Result with UUID {uuid} not found" });
            return Results.Json(result);
        });


        // In-memory session registry for SSE clients
        var sseClients = new ConcurrentDictionary<string, HttpResponse>();

        // SSE endpoint with JSON-RPC handshake
        app.MapGet("/sse", async (HttpContext context) =>
        {
            context.Response.Headers["Content-Type"] = "text/event-stream";
            var sessionId = Guid.NewGuid().ToString();
            sseClients[sessionId] = context.Response;

            // Send JSON-RPC welcome/handshake
            var welcome = new
            {
                jsonrpc = "2.0",
                method = "mcp/handshake",
                @params = new { message = "Welcome to MCP Server (dotnet)", sessionId }
            };
            var welcomeJson = JsonSerializer.Serialize(welcome);
            await context.Response.WriteAsync($"event: message\ndata: {welcomeJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send tools list event
            var toolsList = DOTNETCONVERTED.ToolRegistry.List()
                .Select(t => new { name = t.Name, schema = t.Schema, source = t.Source }).ToList();
            var toolsEvent = new {
                jsonrpc = "2.0",
                method = "tools/list",
                @params = new { tools = toolsList }
            };
            var toolsJson = JsonSerializer.Serialize(toolsEvent);
            await context.Response.WriteAsync($"event: message\ndata: {toolsJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send resources list event
            var resourcesList = DOTNETCONVERTED.ResourceRegistry.List()
                .Select(r => new { name = r.Name, uriPattern = r.UriPattern, source = r.Source }).ToList();
            var resourcesEvent = new {
                jsonrpc = "2.0",
                method = "resources/list",
                @params = new { resources = resourcesList }
            };
            var resourcesJson = JsonSerializer.Serialize(resourcesEvent);
            await context.Response.WriteAsync($"event: message\ndata: {resourcesJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send prompts list event
            var promptsList = DOTNETCONVERTED.PromptRegistry.List()
                .Select(p => new { name = p.Name, schema = p.Schema, source = p.Source }).ToList();
            var promptsEvent = new {
                jsonrpc = "2.0",
                method = "prompts/list",
                @params = new { prompts = promptsList }
            };
            var promptsJson = JsonSerializer.Serialize(promptsEvent);
            await context.Response.WriteAsync($"event: message\ndata: {promptsJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send usage/welcome notification
            var usageEvent = new {
                jsonrpc = "2.0",
                method = "server/usage",
                @params = new {
                    message = "You are connected to the MCP .NET server. Use tools/call, resources/read, or prompts/get via JSON-RPC. See /diagnostic for more info.",
                    example = new {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "tools/call",
                        @params = new { name = toolsList.FirstOrDefault()?.name ?? "mcp_execute_query", arguments = new { sql = "SELECT 1" } },
                        sessionId = sessionId
                    }
                }
            };
            var usageJson = JsonSerializer.Serialize(usageEvent);
            await context.Response.WriteAsync($"event: message\ndata: {usageJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Keep connection open
            try
            {
                while (!context.RequestAborted.IsCancellationRequested)
                {
                    await Task.Delay(15000, context.RequestAborted); // keepalive
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                sseClients.TryRemove(sessionId, out _);
            }
        });
        // SSE endpoint with JSON-RPC handshake
        app.MapPost("/sse", async (HttpContext context) =>
        {
            context.Response.Headers["Content-Type"] = "text/event-stream";
            var sessionId = Guid.NewGuid().ToString();
            sseClients[sessionId] = context.Response;

            // Send JSON-RPC welcome/handshake
            var welcome = new
            {
                jsonrpc = "2.0",
                method = "mcp/handshake",
                @params = new { message = "Welcome to MCP Server (dotnet)", sessionId }
            };
            var welcomeJson = JsonSerializer.Serialize(welcome);
            await context.Response.WriteAsync($"event: message\ndata: {welcomeJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send tools list event
            var toolsList = DOTNETCONVERTED.ToolRegistry.List()
                .Select(t => new { name = t.Name, schema = t.Schema, source = t.Source }).ToList();
            var toolsEvent = new {
                jsonrpc = "2.0",
                method = "tools/list",
                @params = new { tools = toolsList }
            };
            var toolsJson = JsonSerializer.Serialize(toolsEvent);
            await context.Response.WriteAsync($"event: message\ndata: {toolsJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send resources list event
            var resourcesList = DOTNETCONVERTED.ResourceRegistry.List()
                .Select(r => new { name = r.Name, uriPattern = r.UriPattern, source = r.Source }).ToList();
            var resourcesEvent = new {
                jsonrpc = "2.0",
                method = "resources/list",
                @params = new { resources = resourcesList }
            };
            var resourcesJson = JsonSerializer.Serialize(resourcesEvent);
            await context.Response.WriteAsync($"event: message\ndata: {resourcesJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send prompts list event
            var promptsList = DOTNETCONVERTED.PromptRegistry.List()
                .Select(p => new { name = p.Name, schema = p.Schema, source = p.Source }).ToList();
            var promptsEvent = new {
                jsonrpc = "2.0",
                method = "prompts/list",
                @params = new { prompts = promptsList }
            };
            var promptsJson = JsonSerializer.Serialize(promptsEvent);
            await context.Response.WriteAsync($"event: message\ndata: {promptsJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Send usage/welcome notification
            var usageEvent = new {
                jsonrpc = "2.0",
                method = "server/usage",
                @params = new {
                    message = "You are connected to the MCP .NET server. Use tools/call, resources/read, or prompts/get via JSON-RPC. See /diagnostic for more info.",
                    example = new {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "tools/call",
                        @params = new { name = toolsList.FirstOrDefault()?.name ?? "mcp_execute_query", arguments = new { sql = "SELECT 1" } },
                        sessionId = sessionId
                    }
                }
            };
            var usageJson = JsonSerializer.Serialize(usageEvent);
            await context.Response.WriteAsync($"event: message\ndata: {usageJson}\n\n");
            await context.Response.Body.FlushAsync();

            // Keep connection open
            try
            {
                while (!context.RequestAborted.IsCancellationRequested)
                {
                    await Task.Delay(15000, context.RequestAborted); // keepalive
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                sseClients.TryRemove(sessionId, out _);
            }
        });

        // Messages endpoint (JSON-RPC dispatcher)
        app.MapPost("/messages", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            Console.WriteLine($"[MCP] Received /messages POST: {body}");
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(body); } catch { }
            if (doc == null)
            {
                Console.WriteLine("[MCP] Invalid JSON in /messages");
                return Results.Json(new { error = "Invalid JSON" });
            }
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetRawText() : null;
            var method = root.TryGetProperty("method", out var mProp) ? mProp.GetString() : null;
            var @params = root.TryGetProperty("params", out var pProp) ? pProp : default;
            var sessionId = root.TryGetProperty("sessionId", out var sProp) ? sProp.GetString() : null;

            Console.WriteLine($"[MCP] method={method}, id={id}, sessionId={sessionId}");

            // Find SSE client
            if (string.IsNullOrEmpty(sessionId) || !sseClients.TryGetValue(sessionId, out var sseResp))
            {
                Console.WriteLine($"[MCP] Missing or invalid sessionId: {sessionId}");
                return Results.Json(new { error = "Missing or invalid sessionId" });
            }

            async Task SendSseResponse(object resp)
            {
                var json = JsonSerializer.Serialize(resp);
                Console.WriteLine($"[MCP] Sending SSE: {json}");
                await sseResp.WriteAsync($"event: message\ndata: {json}\n\n");
                await sseResp.Body.FlushAsync();
            }

            // JSON-RPC response helpers
            string? idValue = null;
            if (idProp.ValueKind == JsonValueKind.String)
                idValue = idProp.GetString();
            else if (idProp.ValueKind == JsonValueKind.Number)
                idValue = idProp.GetRawText();
            object RpcResult(object? result) => new { jsonrpc = "2.0", id = idValue, result };
            object RpcError(int code, string message, object? data = null) => new { jsonrpc = "2.0", id = idValue, error = new { code, message, data } };

            // Handle methods
            switch (method)
            {
                case "initialize":
                    await SendSseResponse(RpcResult(new { message = "MCP Server initialized", server = "dotnet" }));
                    break;
                case "tools/call":
                    {
                        var toolName = @params.TryGetProperty("name", out var tn) ? tn.GetString() : null;
                        var args = @params.TryGetProperty("arguments", out var a) ? JsonNode.Parse(a.GetRawText()) as JsonObject : new JsonObject();
                        var tool = DOTNETCONVERTED.ToolRegistry.Get(toolName ?? "");
                        if (tool == null)
                        {
                            await SendSseResponse(RpcError(-32601, $"Tool not found: {toolName}"));
                            break;
                        }
                        try
                        {
                            var result = await tool.Handler(args);
                            await SendSseResponse(RpcResult(result));
                        }
                        catch (Exception ex)
                        {
                            await SendSseResponse(RpcError(-32000, ex.Message));
                        }
                        break;
                    }
                case "resources/read":
                    {
                        var resourceName = @params.TryGetProperty("name", out var rn) ? rn.GetString() : null;
                        var args = @params.TryGetProperty("arguments", out var a) ? JsonNode.Parse(a.GetRawText()) as JsonObject : new JsonObject();
                        var resource = DOTNETCONVERTED.ResourceRegistry.Get(resourceName ?? "");
                        if (resource == null)
                        {
                            await SendSseResponse(RpcError(-32601, $"Resource not found: {resourceName}"));
                            break;
                        }
                        try
                        {
                            var result = await resource.Handler(args);
                            await SendSseResponse(RpcResult(result));
                        }
                        catch (Exception ex)
                        {
                            await SendSseResponse(RpcError(-32000, ex.Message));
                        }
                        break;
                    }
                case "prompts/get":
                    {
                        var promptName = @params.TryGetProperty("name", out var pn) ? pn.GetString() : null;
                        var args = @params.TryGetProperty("arguments", out var a) ? JsonNode.Parse(a.GetRawText()) as JsonObject : new JsonObject();
                        var prompt = DOTNETCONVERTED.PromptRegistry.Get(promptName ?? "");
                        if (prompt == null)
                        {
                            await SendSseResponse(RpcError(-32601, $"Prompt not found: {promptName}"));
                            break;
                        }
                        try
                        {
                            var result = await prompt.Handler(args);
                            await SendSseResponse(RpcResult(result));
                        }
                        catch (Exception ex)
                        {
                            await SendSseResponse(RpcError(-32000, ex.Message));
                        }
                        break;
                    }
                default:
                    await SendSseResponse(RpcError(-32601, $"Method not found: {method}"));
                    break;
            }
            return Results.Json(new { status = "dispatched" });
        });
    }

    public class DbConfig
    {
        public string? Server { get; set; }
        public string? Database { get; set; }
        public string? User { get; set; }
        public int? Port { get; set; }
    }

    // Simulated tool registry
    public static class ToolRegistry
    {
        private static readonly List<object> _tools = new()
        {
            new { name = "mcp_execute_query", schema = new { sql = "string" }, source = "internal" },
            new { name = "mcp_table_details", schema = new { tableName = "string" }, source = "internal" },
            new { name = "mcp_cursor_guide", schema = new { random_string = "string?" }, source = "internal" }
        };
        public static List<object> ListTools() => _tools;
    }

    // Simulated query result store
    public static class QueryResultStore
    {
        private static readonly ConcurrentDictionary<string, object> _results = new();
        static QueryResultStore()
        {
            // Add a sample result
            _results["sample-uuid"] = new { metadata = new { uuid = "sample-uuid", timestamp = DateTime.UtcNow, query = "SELECT 1", rowCount = 1 }, data = new[] { new { Test = 1 } } };
        }
        public static List<object> ListResults() => _results.Values.ToList();
        public static object? GetResult(string uuid) => _results.TryGetValue(uuid, out var val) ? val : null;
    }
}
