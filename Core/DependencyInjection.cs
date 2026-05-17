using graphnotelm.Core.Clients;
using graphnotelm.Core.Contexts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace graphnotelm.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddScoped<INoteGraphAccessService, NoteGraphAccessService>();

        services.AddScoped<INoteGraphService, NoteGraphService>();
        services.AddScoped<INoteNodeService, NoteNodeService>();

        services.AddScoped<INoteGraphTagService, NoteGraphTagService>();
        services.AddScoped<INoteGraphRelationshipService, NoteGraphRelationshipService>();

        services.AddScoped<IGraphAnalysisService, GraphAnalysisService>();
        services.AddScoped<ILLMContextBuilder, LLMContextBuilder>();
        services.AddScoped<ILLMAnalysisService, LLMAnalysisService>();
        services.AddScoped<IChatService, ChatService>();

        // Register Clients
        services.AddHttpClient("anthropic", http =>
        {
            http.BaseAddress = new Uri("https://api.anthropic.com/");
            http.DefaultRequestHeaders.Add("x-api-key", configuration["Anthropic:ApiKey"]!);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });
        services.AddSingleton<IChatClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("anthropic");
            return new AnthropicChatClient(http, configuration["Anthropic:Model"] ?? "claude-sonnet-4-20250514");
        });

        // Register LLM Factory
        services.AddScoped<GraphToolFactory>();

        // Register Contexts
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

        // Register JWT configuration
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],

                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // SignalR WebSocket upgrades can't send Authorization headers,
                // so the token arrives as ?access_token= in the query string.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) &&
                            context.Request.Path.StartsWithSegments("/hub/chat"))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
