using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Infrastructure.Repository;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace graphnotelm.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
        {
            // PostgreSQL
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("PrimaryDB")));

            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<INoteGraphMetadataRepository, PostgreSQLNoteGraphMetadataRepository>();

            // NoteGraph repository
            if (env.IsDevelopment())
            {
                if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
                    services.AddSingleton<INoteGraphRepository, JsonFileNoteGraphRepository>();
                else
                    services.AddSingleton<INoteGraphRepository, InMemoryDBNoteGraphRepository>();
            }
            else
            {
                var awsSection = configuration.GetSection("Aws");
                var credentials = new BasicAWSCredentials(
                    awsSection["AccessKey"] ?? throw new InvalidOperationException("Aws:AccessKey missing"),
                    awsSection["SecretKey"] ?? throw new InvalidOperationException("Aws:SecretKey missing")
                );
                var region = RegionEndpoint.GetBySystemName(awsSection["Region"] ?? "us-east-1");

                services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(credentials, region));
                services.Configure<DynamoDbSettings>(configuration.GetSection("DynamoDb"));
                services.AddScoped<INoteGraphRepository, DynamoDBNoteGraphRepository>();
            }

            return services;
        }
    }
}
