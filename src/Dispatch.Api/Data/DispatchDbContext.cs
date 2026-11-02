using Dispatch.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Data;

/// <summary>
/// Shared model for all database providers. Each provider has its own derived context
/// (see <see cref="PostgresDispatchDbContext"/> and <see cref="SqlServerDispatchDbContext"/>)
/// so that each one gets its own migrations folder.
/// </summary>
public class DispatchDbContext : DbContext
{
    public DispatchDbContext(DbContextOptions<DispatchDbContext> options) : base(options) { }

    protected DispatchDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Assignment> Assignments => Set<Assignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Unit>(unit =>
        {
            unit.Property(u => u.CallSign).HasMaxLength(32);
            unit.HasIndex(u => u.CallSign).IsUnique();
            unit.Property(u => u.Type).HasConversion<string>().HasMaxLength(16);
            unit.Property(u => u.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<Incident>(incident =>
        {
            incident.Property(i => i.Title).HasMaxLength(200);
            incident.Property(i => i.Description).HasMaxLength(2000);
            incident.Property(i => i.Address).HasMaxLength(300);
            incident.Property(i => i.Priority).HasConversion<string>().HasMaxLength(16);
            incident.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
            incident.HasIndex(i => i.Status);
        });

        modelBuilder.Entity<Assignment>(assignment =>
        {
            assignment.HasOne(a => a.Incident)
                .WithMany(i => i.Assignments)
                .HasForeignKey(a => a.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            assignment.HasOne(a => a.Unit)
                .WithMany(u => u.Assignments)
                .HasForeignKey(a => a.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public class PostgresDispatchDbContext(DbContextOptions<PostgresDispatchDbContext> options)
    : DispatchDbContext(options);

public class SqlServerDispatchDbContext(DbContextOptions<SqlServerDispatchDbContext> options)
    : DispatchDbContext(options);
