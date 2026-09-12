using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Infrastructure.Repository;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace graphnotelm.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
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
                services.AddSingleton<IImageRepository, LocalImageRepository>();
            }
            else
            {
                // PostgreSQL
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(configuration.GetConnectionString("PrimaryDB")));
                services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<INoteGraphMetadataRepository, PostgreSQLNoteGraphMetadataRepository>();

                // TODO: register PostgreSQL INoteGraphRepository and INoteNodeRepository implementations
                // TODO: register a cloud IImageRepository implementation (e.g. object storage + PostgreSQL)
            }

            return services;
        }
    }
}
