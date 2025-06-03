


using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DOTNETCONVERTED;

var builder = WebApplication.CreateBuilder(args);

// Register core MCP database tools
McpDatabaseTools.RegisterAll();

// Register core MCP resources
ResourceRegistry.Register(
    "schema",
    "/schema",
    async args => new System.Text.Json.Nodes.JsonObject { ["schema"] = "(schema resource placeholder)" }
);
ResourceRegistry.Register(
    "tables",
    "/tables",
    async args => new System.Text.Json.Nodes.JsonObject { ["tables"] = "(tables resource placeholder)" }
);

// Register core MCP prompts
PromptRegistry.Register(
    "generate-query",
    new System.Text.Json.Nodes.JsonObject { ["description"] = "string" },
    async args => new System.Text.Json.Nodes.JsonObject { ["query"] = "(generate-query prompt placeholder)" }
);
PromptRegistry.Register(
    "explain-query",
    new System.Text.Json.Nodes.JsonObject { ["sql"] = "string" },
    async args => new System.Text.Json.Nodes.JsonObject { ["explanation"] = "(explain-query prompt placeholder)" }
);


// Register core MCP database tools
McpDatabaseTools.RegisterAll();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>

{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Add basic rate limiting (per IP, 60 req/min)
builder.Services.AddRateLimiter(_ =>
    _.AddFixedWindowLimiter(policyName: "default", options =>
    {
        options.PermitLimit = 60;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 0;
    })
);

// TODO: Add rate limiting and security middleware here

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();

// Map MCP endpoints
app.MapMcpEndpoints();

app.Run();
