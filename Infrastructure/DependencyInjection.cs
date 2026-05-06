using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;


namespace graphnotelm.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<INoteGraphRepository, DynamoDBNoteGraphRepository>();
            services.AddScoped<INoteGraphMetadataRepository, PostgreSQLNoteGraphMetadataRepository>();
            return services;
        }
    }
}
