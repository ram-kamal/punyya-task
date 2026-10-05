using Microsoft.EntityFrameworkCore;
using PHF.Core.Entities;
using PHF.Core.Enums;

namespace PHF.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AppActivityLog> AppActivityLogs => Set<AppActivityLog>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<TaskBackup> TaskBackups => Set<TaskBackup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User indexes and relationships
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.StationId);

            entity.HasMany(u => u.AssignedTasks)
                  .WithOne(t => t.AssignedToUser)
                  .HasForeignKey(t => t.AssignedTo)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(u => u.CreatedTasks)
                  .WithOne(t => t.AssignedByUser)
                  .HasForeignKey(t => t.AssignedBy)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(u => u.Shifts)
                  .WithOne(s => s.User)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.UserSessions)
                  .WithOne(s => s.User)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(u => u.AppActivityLogs)
                  .WithOne(l => l.User)
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Tasks
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasIndex(t => t.TaskCode).IsUnique();
            entity.HasIndex(t => new { t.AssignedTo, t.Status });

            entity.HasMany(t => t.Attachments)
                  .WithOne(a => a.Task)
                  .HasForeignKey(a => a.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.ActivityLogs)
                  .WithOne(l => l.Task)
                  .HasForeignKey(l => l.TaskId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // App Activity Logs
        modelBuilder.Entity<AppActivityLog>(entity =>
        {
            entity.HasIndex(a => new { a.UserId, a.LoggedAt });
            entity.HasIndex(a => a.TaskId);
        });

        // User Sessions
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasIndex(s => new { s.UserId, s.EventTimestamp });
        });

        // Shifts
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasIndex(s => new { s.UserId, s.ClockIn });
        });

        // Leave Requests
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasIndex(l => new { l.UserId, l.Status });
            entity.HasIndex(l => new { l.StartDate, l.EndDate });
            entity.HasOne(l => l.User)
                  .WithMany(u => u.LeaveRequests)
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Reviewer)
                  .WithMany()
                  .HasForeignKey(l => l.ReviewedBy)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Enforce DateTimeKind.Utc on all DateTime properties across all entities (essential for PostgreSQL timestamp with time zone)
        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc)),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }

    public async Task EnsureSeedDataAsync()
    {
        var adminEmail = (Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL") ?? "dhyanidhvaj@gmail.com").Trim().ToLowerInvariant();
        var adminUsername = (Environment.GetEnvironmentVariable("DEFAULT_ADMIN_USERNAME") ?? "dhvaj").Trim();
        var adminPassword = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_PASSWORD") ?? "Admin0246";
        var adminFullName = (Environment.GetEnvironmentVariable("DEFAULT_ADMIN_NAME") ?? adminUsername).Trim();

        var hasAdmin = await Users.AnyAsync(u => u.Role == UserRole.ADMIN.ToString() 
                                              || (u.Username != null && u.Username.ToLower() == adminUsername.ToLower()) 
                                              || u.Email.ToLower() == adminEmail);
        if (!hasAdmin)
        {
            var admin = new User
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                Email = adminEmail,
                Username = adminUsername,
                FullName = adminFullName,
                Role = UserRole.ADMIN.ToString(),
                StationId = "ST-ADMIN",
                WorkMode = "IN_OFFICE",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                CreatedAt = DateTime.UtcNow
            };

            await Users.AddAsync(admin);
            await SaveChangesAsync();
        }
    }
}
