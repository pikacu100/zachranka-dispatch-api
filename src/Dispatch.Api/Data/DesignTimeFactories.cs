using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dispatch.Api.Data;

// Used only by `dotnet ef migrations add`. Creating a migration doesn't connect to a database,
// so the connection strings here are placeholders.

public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresDispatchDbContext>
{
    public PostgresDispatchDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresDispatchDbContext>()
            .UseNpgsql("Host=localhost;Database=dispatch")
            .Options);
}

public class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerDispatchDbContext>
{
    public SqlServerDispatchDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerDispatchDbContext>()
            .UseSqlServer("Server=localhost;Database=dispatch")
            .Options);
}
