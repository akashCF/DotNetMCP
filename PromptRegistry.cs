using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DOTNETCONVERTED;

public class McpPrompt
{
    public string Name { get; set; } = string.Empty;
    public JsonObject Schema { get; set; } = new();
    [JsonIgnore]
    public Func<JsonObject, Task<JsonObject>> Handler { get; set; } = _ => Task.FromResult(new JsonObject());
    public string Source { get; set; } = "internal";
}

public static class PromptRegistry
{
    private static readonly Dictionary<string, McpPrompt> _prompts = new();

    public static void Register(string name, JsonObject schema, Func<JsonObject, Task<JsonObject>> handler, string source = "internal")
    {
        var prompt = new McpPrompt { Name = name, Schema = schema, Handler = handler, Source = source };
        _prompts[name] = prompt;
    }

    public static McpPrompt? Get(string name)
    {
        if (_prompts.TryGetValue(name, out var prompt))
            return prompt;
        // Try mcp_ and mcp_SQL_ aliases
        if (name.StartsWith("mcp_"))
            _prompts.TryGetValue(name.Replace("mcp_", "mcp_SQL_"), out prompt);
        else if (name.StartsWith("mcp_SQL_"))
            _prompts.TryGetValue(name.Replace("mcp_SQL_", "mcp_"), out prompt);
        return prompt;
    }

    public static List<McpPrompt> List() => _prompts.Values.ToList();
}
