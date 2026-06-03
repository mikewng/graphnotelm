using graphnotelm.API;
using graphnotelm.Core;
using graphnotelm.Core.Models;
using graphnotelm.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// ── Appdata key directory ────────────────────────────────────────────────────
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var keyDir  = Path.Combine(appData, "graphnotelm");
Directory.CreateDirectory(keyDir);

// Auto-generate and persist a JWT key for local mode if none is configured
if (string.IsNullOrEmpty(builder.Configuration["Jwt:Key"]))
{
    var keyFile = Path.Combine(keyDir, "jwt.key");
    var jwtKey  = File.Exists(keyFile)
        ? File.ReadAllText(keyFile).Trim()
        : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    File.WriteAllText(keyFile, jwtKey);
    builder.Configuration["Jwt:Key"] = jwtKey;
}

// Generate and persist an MCP secret key
var mcpKeyFile  = Path.Combine(keyDir, "mcp.key");
var mcpKey      = File.Exists(mcpKeyFile)
    ? File.ReadAllText(mcpKeyFile).Trim()
    : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
File.WriteAllText(mcpKeyFile, mcpKey);

// Load persisted MCP local user ID (set after first login via /settings/mcp/configure-user)
var mcpUserFile = Path.Combine(keyDir, "mcp-user.txt");
Guid? mcpUserId = File.Exists(mcpUserFile) && Guid.TryParse(File.ReadAllText(mcpUserFile).Trim(), out var parsedId)
    ? parsedId
    : null;

var mcpSettings = new McpSettings
{
    SecretKey    = mcpKey,
    LocalUserId  = mcpUserId,
    UserFilePath = mcpUserFile
};
builder.Services.AddSingleton(mcpSettings);

// ── Localhost-only binding in local (Electron) mode ──────────────────────────
var localDb = builder.Configuration.GetConnectionString("LocalDB");
if (localDb != null && !builder.Environment.IsDevelopment())
{
    var port = builder.Configuration.GetValue<int>("LocalPort", 5240);
    builder.WebHost.UseUrls($"http://localhost:{port}");
}

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Paste your JWT token here."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ── MCP secret key middleware (runs before MapMcp) ───────────────────────────
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        var provided = context.Request.Query["key"].ToString();
        if (string.IsNullOrEmpty(provided) || provided != mcpSettings.SecretKey)
        {
            context.Response.StatusCode  = 401;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("MCP: invalid or missing key.");
            return;
        }
    }
    await next();
});

// ── Route mapping ─────────────────────────────────────────────────────────────
app.MapControllers();
app.MapHub<AIChatHub>("/hub/chat");
app.MapMcp("/mcp");

// ── Database init ─────────────────────────────────────────────────────────────
if (localDb != null)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
else if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() && localDb == null)
    app.UseHttpsRedirection();

app.UseCors("LocalFrontend");
app.UseAuthorization();

app.Run();
