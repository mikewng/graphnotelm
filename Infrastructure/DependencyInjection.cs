using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using graphnotelm.Infrastructure.Repository;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace graphnotelm.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // PostgreSQL
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<INoteGraphMetadataRepository, PostgreSQLNoteGraphMetadataRepository>();

            // DynamoDB
            var awsSection = configuration.GetSection("Aws");
            var credentials = new BasicAWSCredentials(
                awsSection["AccessKey"] ?? throw new InvalidOperationException("Aws:AccessKey missing"),
                awsSection["SecretKey"] ?? throw new InvalidOperationException("Aws:SecretKey missing")
            );
            var region = RegionEndpoint.GetBySystemName(awsSection["Region"] ?? "us-east-1");

            services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(credentials, region));
            services.Configure<DynamoDbSettings>(configuration.GetSection("DynamoDb"));
            services.AddScoped<INoteGraphRepository, DynamoDBNoteGraphRepository>();

            return services;
        }
    }
}
