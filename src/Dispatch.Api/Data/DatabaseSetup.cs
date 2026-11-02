using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Registers the DbContext for the provider set in "Database:Provider":
    /// Postgres, SqlServer or Sqlite (local development without Docker).
    /// The rest of the app only depends on <see cref="DispatchDbContext"/>.
    /// </summary>
    public static IServiceCollection AddDispatchDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "Sqlite";

        switch (provider)
        {
            case "Postgres":
                services.AddDbContext<PostgresDispatchDbContext>(o =>
                    o.UseNpgsql(config.GetConnectionString("Postgres")));
                services.AddScoped<DispatchDbContext>(sp => sp.GetRequiredService<PostgresDispatchDbContext>());
                break;
            case "SqlServer":
                services.AddDbContext<SqlServerDispatchDbContext>(o =>
                    o.UseSqlServer(config.GetConnectionString("SqlServer")));
                services.AddScoped<DispatchDbContext>(sp => sp.GetRequiredService<SqlServerDispatchDbContext>());
                break;
            case "Sqlite":
                services.AddDbContext<DispatchDbContext>(o =>
                    o.UseSqlite(config.GetConnectionString("Sqlite")));
                break;
            default:
                throw new InvalidOperationException($"Unknown Database:Provider '{provider}'.");
        }

        return services;
    }

    /// <summary>
    /// Applies migrations (Postgres, SqlServer) or creates the schema (Sqlite has no migrations here).
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

        if (db.Database.IsSqlite())
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();
    }
}
