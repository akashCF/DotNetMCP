using Microsoft.Data.SqlClient;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace DOTNETCONVERTED;

public static class McpDatabaseTools
{
    public static void RegisterAll()
    {
        // Register execute_query tool and alias
        ToolRegistry.Register(
            "mcp_execute_query",
            new JsonObject {
                ["sql"] = "string (required)",
                ["maxRows"] = "int (optional, default 1000)"
            },
            async args => {
                var sql = args["sql"]?.ToString() ?? "";
                var maxRows = args["maxRows"]?.GetValue<int?>() ?? 1000;
                var result = new JsonObject();
                try
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.Development.json", optional: true)
                        .AddEnvironmentVariables()
                        .Build();
                    var connStr = config["Database:ConnectionString"] ?? Environment.GetEnvironmentVariable("MCP_SQL_CONNECTION") ?? "";
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.CommandTimeout = 30;
                    using var reader = await cmd.ExecuteReaderAsync();
                    var rows = new List<Dictionary<string, object?>>();
                    int rowCount = 0;
                    while (await reader.ReadAsync() && rowCount < maxRows)
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                        rowCount++;
                    }
                    result["rowCount"] = rowCount;
                    result["results"] = JsonNode.Parse(JsonSerializer.Serialize(rows));
                }
                catch (Exception ex)
                {
                    result["error"] = ex.Message;
                }
                return result;
            }
        );
        ToolRegistry.Register(
            "mcp_SQL_execute_query",
            new JsonObject {
                ["sql"] = "string (required)",
                ["maxRows"] = "int (optional, default 1000)"
            },
            async args => await ToolRegistry.Get("mcp_execute_query")!.Handler(args)
        );

        // Table details tool
        ToolRegistry.Register(
            "mcp_table_details",
            new JsonObject { ["tableName"] = "string (required)" },
            async args => new JsonObject { ["details"] = "(table details placeholder)" }
        );
        ToolRegistry.Register(
            "mcp_SQL_table_details",
            new JsonObject { ["tableName"] = "string (required)" },
            async args => await ToolRegistry.Get("mcp_table_details")!.Handler(args)
        );

        // Discover tables tool
        ToolRegistry.Register(
            "mcp_discover_tables",
            new JsonObject { },
            async args => new JsonObject { ["tables"] = "(discover tables placeholder)" }
        );
        ToolRegistry.Register(
            "mcp_SQL_discover_tables",
            new JsonObject { },
            async args => await ToolRegistry.Get("mcp_discover_tables")!.Handler(args)
        );

        // Paginated query tool
        ToolRegistry.Register(
            "mcp_paginated_query",
            new JsonObject {
                ["sql"] = "string (required)",
                ["page"] = "int (required)",
                ["pageSize"] = "int (required)"
            },
            async args => new JsonObject { ["results"] = "(paginated query placeholder)" }
        );
        ToolRegistry.Register(
            "mcp_SQL_paginated_query",
            new JsonObject {
                ["sql"] = "string (required)",
                ["page"] = "int (required)",
                ["pageSize"] = "int (required)"
            },
            async args => await ToolRegistry.Get("mcp_paginated_query")!.Handler(args)
        );

        // Cursor guide tool
        ToolRegistry.Register(
            "mcp_cursor_guide",
            new JsonObject { ["random_string"] = "string?" },
            async args => new JsonObject { ["guide"] = "(cursor guide placeholder)" }
        );
        ToolRegistry.Register(
            "mcp_SQL_cursor_guide",
            new JsonObject { ["random_string"] = "string?" },
            async args => await ToolRegistry.Get("mcp_cursor_guide")!.Handler(args)
        );

        // Add more tools and aliases as needed (procedure_details, function_details, view_details, index_details, etc.)
    }
}
