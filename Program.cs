using graphnotelm.API;
using graphnotelm.Core;
using graphnotelm.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Auto-generate and persist a JWT key for local mode if none is configured
if (string.IsNullOrEmpty(builder.Configuration["Jwt:Key"]))
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var keyDir = Path.Combine(appData, "graphnotelm");
    Directory.CreateDirectory(keyDir);
    var keyFile = Path.Combine(keyDir, "jwt.key");

    var jwtKey = File.Exists(keyFile)
        ? File.ReadAllText(keyFile).Trim()
        : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    File.WriteAllText(keyFile, jwtKey);
    builder.Configuration["Jwt:Key"] = jwtKey;
}

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
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
                    Id = "Bearer"
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

// Service registration

// Route mapping (alongside your controller mapping)
app.MapControllers();
app.MapHub<AIChatHub>("/hub/chat");

var localDb = builder.Configuration.GetConnectionString("LocalDB");
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
{
    app.UseHttpsRedirection();
}
app.UseCors("LocalFrontend");
app.UseAuthorization();

app.Run();
