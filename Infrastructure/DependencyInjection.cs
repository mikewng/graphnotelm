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
            var localDb = configuration.GetConnectionString("LocalDB");

            if (localDb != null)
            {
                // Expand %APPDATA% and other env vars, then ensure the directory exists
                var expandedLocalDb = Environment.ExpandEnvironmentVariables(localDb);
                var dbPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(expandedLocalDb).DataSource;
                var dbDir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDir))
                    Directory.CreateDirectory(dbDir);

                // Local / Electron mode — SQLite, no cloud dependencies
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(expandedLocalDb));
                services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
                services.AddScoped<IUserRepository, SQLiteUserRepository>();
                services.AddScoped<INoteGraphMetadataRepository, SQLiteNoteGraphMetadataRepository>();
                services.AddSingleton<INoteGraphRepository, SQLiteNoteGraphRepository>();
                services.AddSingleton<INoteNodeRepository, SQLiteNoteNodeRepository>();
            }
            else
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
                    services.AddSingleton<INoteGraphRepository, InMemoryDBNoteGraphRepository>();
                    services.AddSingleton<INoteNodeRepository, InMemoryDBNoteNodeRepository>();
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
                    services.Configure<DynamoDbNodeSettings>(configuration.GetSection("DynamoDbNodes"));
                    services.AddScoped<INoteGraphRepository, DynamoDBNoteGraphRepository>();
                    services.AddScoped<INoteNodeRepository, DynamoDBNoteNodeRepository>();
                }
            }

            return services;
        }
    }
}
