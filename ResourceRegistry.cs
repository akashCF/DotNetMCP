using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DOTNETCONVERTED;

public class McpResource
{
    public string Name { get; set; } = string.Empty;
    public string UriPattern { get; set; } = string.Empty;
    [JsonIgnore]
    public Func<JsonObject, Task<JsonObject>> Handler { get; set; } = _ => Task.FromResult(new JsonObject());
    public string Source { get; set; } = "internal";
}

public static class ResourceRegistry
{
    private static readonly Dictionary<string, McpResource> _resources = new();

    public static void Register(string name, string uriPattern, Func<JsonObject, Task<JsonObject>> handler, string source = "internal")
    {
        var resource = new McpResource { Name = name, UriPattern = uriPattern, Handler = handler, Source = source };
        _resources[name] = resource;
    }

    public static McpResource? Get(string name)
    {
        if (_resources.TryGetValue(name, out var resource))
            return resource;
        // Try mcp_ and mcp_SQL_ aliases
        if (name.StartsWith("mcp_"))
            _resources.TryGetValue(name.Replace("mcp_", "mcp_SQL_"), out resource);
        else if (name.StartsWith("mcp_SQL_"))
            _resources.TryGetValue(name.Replace("mcp_SQL_", "mcp_"), out resource);
        return resource;
    }

    public static List<McpResource> List() => _resources.Values.ToList();
}
