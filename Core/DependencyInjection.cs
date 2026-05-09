using graphnotelm.Core.Contexts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
            });

        services.AddAuthorization();

        return services;
    }
}
