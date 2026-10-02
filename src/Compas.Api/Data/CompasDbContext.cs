using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Data;

public class CompasDbContext : DbContext
{
    public CompasDbContext(DbContextOptions<CompasDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<PlanRun> PlanRuns => Set<PlanRun>();
    public DbSet<PlanEntry> PlanEntries => Set<PlanEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.ColorHex).HasMaxLength(9);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<WorkItem>(e =>
        {
            e.Property(w => w.Title).IsRequired().HasMaxLength(300);
            e.Property(w => w.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(w => w.Project)
             .WithMany(p => p.WorkItems)
             .HasForeignKey(w => w.ProjectId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(w => w.Status);
            e.HasIndex(w => w.DueDate);
        });

        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.Property(c => c.Title).IsRequired().HasMaxLength(300);
            e.Property(c => c.Source).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(c => c.StartTime);
        });

        modelBuilder.Entity<PlanRun>(e =>
        {
            e.HasIndex(p => p.PlanDate);
        });

        modelBuilder.Entity<PlanEntry>(e =>
        {
            e.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Reason).HasMaxLength(500);

            e.HasOne(p => p.PlanRun)
             .WithMany(r => r.Entries)
             .HasForeignKey(p => p.PlanRunId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.WorkItem)
             .WithMany()
             .HasForeignKey(p => p.WorkItemId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(p => p.CalendarEvent)
             .WithMany()
             .HasForeignKey(p => p.CalendarEventId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
