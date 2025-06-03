using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DOTNETCONVERTED;

public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public JsonObject Schema { get; set; } = new();
    [JsonIgnore]
    public Func<JsonObject, Task<JsonObject>> Handler { get; set; } = _ => Task.FromResult(new JsonObject());
    public string Source { get; set; } = "internal";
}

public static class ToolRegistry
{
    private static readonly Dictionary<string, McpTool> _tools = new();

    public static void Register(string name, JsonObject schema, Func<JsonObject, Task<JsonObject>> handler, string source = "internal")
    {
        var tool = new McpTool { Name = name, Schema = schema, Handler = handler, Source = source };
        _tools[name] = tool;
    }

    public static McpTool? Get(string name)
    {
        if (_tools.TryGetValue(name, out var tool)) return tool;
        // Try mcp_ and mcp_SQL_ aliases
        if (name.StartsWith("mcp_"))
            _tools.TryGetValue(name.Replace("mcp_", "mcp_SQL_"), out tool);
        else if (name.StartsWith("mcp_SQL_"))
            _tools.TryGetValue(name.Replace("mcp_SQL_", "mcp_"), out tool);
        return tool;
    }

    public static List<McpTool> List() => _tools.Values.ToList();
}
