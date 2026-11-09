using Compas.Api.Models;
using Compas.Api.Services;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Data;

public class CompasDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly CurrentUser _currentUser;

    public CompasDbContext(DbContextOptions<CompasDbContext> options, CurrentUser currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    /// <summary>Usuario de la sesión; los filtros de consulta lo leen en cada consulta (0 = sin sesión: no se ve nada).</summary>
    private int CurrentUserId => _currentUser.UserId;

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<PlanRun> PlanRuns => Set<PlanRun>();
    public DbSet<PlanEntry> PlanEntries => Set<PlanEntry>();
    public DbSet<GoogleAccount> GoogleAccounts => Set<GoogleAccount>();
    public DbSet<GoogleCalendarSelection> GoogleCalendarSelections => Set<GoogleCalendarSelection>();
    public DbSet<AppUser> Users => Set<AppUser>();

    /// <summary>Claves de Data Protection (ver Program.cs): no pertenecen a ningún usuario.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cada cuenta ve solo lo suyo: filtro automático en TODAS las consultas de estas entidades.
        modelBuilder.Entity<Project>().HasQueryFilter(x => x.UserId == CurrentUserId);
        modelBuilder.Entity<WorkItem>().HasQueryFilter(x => x.UserId == CurrentUserId);
        modelBuilder.Entity<CalendarEvent>().HasQueryFilter(x => x.UserId == CurrentUserId);
        modelBuilder.Entity<PlanRun>().HasQueryFilter(x => x.UserId == CurrentUserId);
        modelBuilder.Entity<PlanEntry>().HasQueryFilter(x => x.PlanRun!.UserId == CurrentUserId);
        modelBuilder.Entity<GoogleAccount>().HasQueryFilter(x => x.UserId == CurrentUserId);
        modelBuilder.Entity<GoogleCalendarSelection>().HasQueryFilter(x => x.UserId == CurrentUserId);

        modelBuilder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.UserId);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.ColorHex).HasMaxLength(9);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<WorkItem>(e =>
        {
            e.HasIndex(w => w.UserId);
            e.Property(w => w.Title).IsRequired().HasMaxLength(300);
            e.Property(w => w.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(w => w.Project)
             .WithMany(p => p.WorkItems)
             .HasForeignKey(w => w.ProjectId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(w => w.Parent)
             .WithMany(w => w.Subtasks)
             .HasForeignKey(w => w.ParentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(w => w.Status);
            e.HasIndex(w => w.DueDate);
        });

        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(c => c.UserId);
            e.Property(c => c.Title).IsRequired().HasMaxLength(300);
            e.Property(c => c.Source).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(c => c.StartTime);
            e.Property(c => c.ExternalId).HasMaxLength(1024);
            e.Property(c => c.ExternalCalendarId).HasMaxLength(400);
            e.HasIndex(c => new { c.ExternalCalendarId, c.ExternalId });
        });

        modelBuilder.Entity<GoogleAccount>(e =>
        {
            e.HasIndex(g => g.UserId);
            e.Property(g => g.Email).IsRequired().HasMaxLength(320);
            e.Property(g => g.RefreshToken).IsRequired();
            e.Property(g => g.DefaultCalendarId).HasMaxLength(400);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("AppUsers");
            e.Property(u => u.EmailVerified).HasDefaultValue(false);
            e.Property(u => u.Email).IsRequired().HasMaxLength(320);
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.GoogleSubject).HasMaxLength(100);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.GoogleSubject);
        });

        modelBuilder.Entity<GoogleCalendarSelection>(e =>
        {
            e.Property(g => g.CalendarId).IsRequired().HasMaxLength(400);
            e.Property(g => g.Summary).IsRequired().HasMaxLength(300);
            e.Property(g => g.ColorHex).HasMaxLength(9);
            e.HasIndex(g => new { g.UserId, g.CalendarId }).IsUnique(); // cada usuario tiene su lista de calendarios
        });

        modelBuilder.Entity<PlanRun>(e =>
        {
            e.HasIndex(p => p.UserId);
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

    // Los registros nuevos se asignan al usuario de la sesión; sin sesión no se puede crear ninguno.
    private void StampOwner()
    {
        foreach (var entry in ChangeTracker.Entries<IUserOwned>().Where(e => e.State == EntityState.Added && e.Entity.UserId == 0))
        {
            var userId = CurrentUserId;
            if (userId == 0) throw new InvalidOperationException("No hay una sesión iniciada para asignar el propietario del nuevo registro.");
            entry.Entity.UserId = userId;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOwner();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwner();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
