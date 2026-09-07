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
    }

    public async Task EnsureSeedDataAsync()
    {
        if (!await LeaveRequests.AnyAsync() && await Users.AnyAsync())
        {
            var st101 = await Users.FirstOrDefaultAsync(u => u.StationId == "ST-101");
            var st102 = await Users.FirstOrDefaultAsync(u => u.StationId == "ST-102");
            if (st101 != null && st102 != null)
            {
                var initialLeaves = new List<LeaveRequest>
                {
                    new()
                    {
                        Id = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
                        UserId = st101.Id,
                        LeaveType = LeaveType.CASUAL.ToString(),
                        StartDate = DateTime.UtcNow.Date.AddDays(3),
                        EndDate = DateTime.UtcNow.Date.AddDays(4),
                        TotalDays = 2,
                        Reason = "Family function and personal appointment",
                        Status = LeaveStatus.PENDING.ToString(),
                        CreatedAt = DateTime.UtcNow.AddHours(-3)
                    },
                    new()
                    {
                        Id = Guid.Parse("c0000000-0000-0000-0000-000000000002"),
                        UserId = st102.Id,
                        LeaveType = LeaveType.SICK.ToString(),
                        StartDate = DateTime.UtcNow.Date.AddDays(1),
                        EndDate = DateTime.UtcNow.Date.AddDays(1),
                        TotalDays = 1,
                        Reason = "Medical checkup and dental consultation",
                        Status = LeaveStatus.PENDING.ToString(),
                        CreatedAt = DateTime.UtcNow.AddHours(-1)
                    }
                };
                await LeaveRequests.AddRangeAsync(initialLeaves);
                await SaveChangesAsync();
            }
        }

        if (await Users.AnyAsync())
        {
            var existingUsers = await Users.Where(u => string.IsNullOrEmpty(u.Username)).ToListAsync();
            if (existingUsers.Any())
            {
                foreach (var u in existingUsers)
                {
                    u.Username = u.Email.Split('@')[0];
                    if (string.IsNullOrEmpty(u.WorkMode)) u.WorkMode = "IN_OFFICE";
                }
                await SaveChangesAsync();
            }
            return;
        }

        // BCrypt hash for "Password@123" and "Admin@123"
        // Generate a standard test hash using BCrypt
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");

        var adminId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
        var managerId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
        var st101Id = Guid.Parse("a0000000-0000-0000-0000-000000000003");
        var st102Id = Guid.Parse("a0000000-0000-0000-0000-000000000004");
        var st103Id = Guid.Parse("a0000000-0000-0000-0000-000000000005");
        var st104Id = Guid.Parse("a0000000-0000-0000-0000-000000000006");

        var users = new List<User>
        {
            new() { Id = adminId, Email = "admin@punnyaheart.org", Username = "admin", PasswordHash = defaultPasswordHash, FullName = "System Administrator", Role = UserRole.ADMIN.ToString(), StationId = "ST-ADMIN", WorkMode = "IN_OFFICE" },
            new() { Id = managerId, Email = "manager@punnyaheart.org", Username = "manager", PasswordHash = defaultPasswordHash, FullName = "Operations Manager", Role = UserRole.MANAGER.ToString(), StationId = "ST-MGR", WorkMode = "IN_OFFICE" },
            new() { Id = st101Id, Email = "st101@punnyaheart.org", Username = "st101", PasswordHash = defaultPasswordHash, FullName = "Aarav Sharma", Role = UserRole.EMPLOYEE.ToString(), StationId = "ST-101", WorkMode = "IN_OFFICE" },
            new() { Id = st102Id, Email = "st102@punnyaheart.org", Username = "st102", PasswordHash = defaultPasswordHash, FullName = "Priya Patel", Role = UserRole.EMPLOYEE.ToString(), StationId = "ST-102", WorkMode = "IN_OFFICE" },
            new() { Id = st103Id, Email = "st103@punnyaheart.org", Username = "st103", PasswordHash = defaultPasswordHash, FullName = "Rahul Verma", Role = UserRole.EMPLOYEE.ToString(), StationId = "ST-103", WorkMode = "IN_OFFICE" },
            new() { Id = st104Id, Email = "st104@punnyaheart.org", Username = "st104", PasswordHash = defaultPasswordHash, FullName = "Sneha Reddy", Role = UserRole.EMPLOYEE.ToString(), StationId = "ST-104", WorkMode = "IN_OFFICE" },
        };

        await Users.AddRangeAsync(users);
        await SaveChangesAsync();

        // Seed initial tasks matching the wireframe in punnya-combined-spec.pptx
        var tasks = new List<TaskItem>
        {
            new()
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                TaskCode = "TASK-108",
                Title = "Configure Backup Scripts",
                Description = "Automate daily database snapshot replication to cloud object storage.",
                AssignedTo = st101Id,
                AssignedBy = managerId,
                Status = TaskItemStatus.COMPLETED.ToString(),
                Priority = TaskPriority.HIGH.ToString(),
                DeliverableRequired = true,
                DeliverableFileName = "backup_config.zip",
                DeliverableUrl = "/storage/deliverables/backup_config.zip",
                StartedAt = DateTime.UtcNow.AddHours(-6),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddHours(-7)
            },
            new()
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000002"),
                TaskCode = "TASK-109",
                Title = "Edit Diet Education Video",
                Description = "Color grade and splice b-roll footage for patient cardiac diet regimen.",
                AssignedTo = st101Id,
                AssignedBy = managerId,
                Status = TaskItemStatus.IN_PROGRESS.ToString(),
                Priority = TaskPriority.CRITICAL.ToString(),
                DeliverableRequired = true,
                StartedAt = DateTime.UtcNow.AddHours(-3),
                CreatedAt = DateTime.UtcNow.AddHours(-4)
            },
            new()
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000003"),
                TaskCode = "TASK-110",
                Title = "Draft Technical Support Guide",
                Description = "Write comprehensive troubleshooting documentation for new telemetry workstations.",
                AssignedTo = st102Id,
                AssignedBy = managerId,
                Status = TaskItemStatus.ASSIGNED.ToString(),
                Priority = TaskPriority.MEDIUM.ToString(),
                DeliverableRequired = true,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            }
        };

        await Tasks.AddRangeAsync(tasks);
        await SaveChangesAsync();

        // Seed initial shifts for today
        var todayMorning = DateTime.UtcNow.Date.AddHours(9);
        var shifts = new List<Shift>
        {
            new() { UserId = st101Id, StationId = "ST-101", ClockIn = todayMorning, TotalActiveSeconds = 15720, TotalIdleSeconds = 900, Status = ShiftStatus.ACTIVE.ToString() },
            new() { UserId = st102Id, StationId = "ST-102", ClockIn = todayMorning.AddMinutes(15), TotalActiveSeconds = 14200, TotalIdleSeconds = 1200, Status = ShiftStatus.ACTIVE.ToString() },
            new() { UserId = st103Id, StationId = "ST-103", ClockIn = todayMorning.AddMinutes(30), TotalActiveSeconds = 11000, TotalIdleSeconds = 3600, Status = ShiftStatus.ACTIVE.ToString() },
            new() { UserId = st104Id, StationId = "ST-104", ClockIn = todayMorning.AddMinutes(45), ClockOut = todayMorning.AddHours(4), TotalActiveSeconds = 11700, TotalIdleSeconds = 1800, Status = ShiftStatus.COMPLETED.ToString() }
        };
        await Shifts.AddRangeAsync(shifts);

        // Seed some activity logs for realistic charts
        var logs = new List<AppActivityLog>
        {
            new() { UserId = st101Id, TaskId = tasks[1].Id, ProcessName = "Adobe Premiere Pro", WindowTitle = "Premiere Pro - Cardiac_Diet_v2.prproj", DurationSeconds = 3600, IsIdle = false, LoggedAt = DateTime.UtcNow.AddMinutes(-35) },
            new() { UserId = st101Id, TaskId = tasks[1].Id, ProcessName = "Google Chrome", WindowTitle = "Research - Nutrition Assets - Google Chrome", DurationSeconds = 1200, IsIdle = false, LoggedAt = DateTime.UtcNow.AddMinutes(-20) },
            new() { UserId = st102Id, TaskId = tasks[2].Id, ProcessName = "Visual Studio Code", WindowTitle = "support-guide.md - VS Code", DurationSeconds = 4800, IsIdle = false, LoggedAt = DateTime.UtcNow.AddMinutes(-15) },
            new() { UserId = st103Id, ProcessName = "Slack", WindowTitle = "Slack | #general | Punnya Heart", DurationSeconds = 600, IsIdle = false, LoggedAt = DateTime.UtcNow.AddMinutes(-40) },
            new() { UserId = st103Id, ProcessName = "System Idle", WindowTitle = "LockApp", DurationSeconds = 900, IsIdle = true, LoggedAt = DateTime.UtcNow.AddMinutes(-25) }
        };
        await AppActivityLogs.AddRangeAsync(logs);

        // Seed some user sessions
        var sessions = new List<UserSession>
        {
            new() { UserId = st101Id, MachineName = "ST-101-DESK", EventType = SessionEventType.LOGON.ToString(), EventTimestamp = todayMorning },
            new() { UserId = st102Id, MachineName = "ST-102-DESK", EventType = SessionEventType.LOGON.ToString(), EventTimestamp = todayMorning.AddMinutes(15) },
            new() { UserId = st103Id, MachineName = "ST-103-DESK", EventType = SessionEventType.LOGON.ToString(), EventTimestamp = todayMorning.AddMinutes(30) },
            new() { UserId = st104Id, MachineName = "ST-104-DESK", EventType = SessionEventType.LOGON.ToString(), EventTimestamp = todayMorning.AddMinutes(45) },
            new() { UserId = st104Id, MachineName = "ST-104-DESK", EventType = SessionEventType.LOCK.ToString(), EventTimestamp = todayMorning.AddHours(4) }
        };
        await UserSessions.AddRangeAsync(sessions);

        // Seed some initial leave requests
        var seedLeaves = new List<LeaveRequest>
        {
            new()
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
                UserId = st101Id,
                LeaveType = LeaveType.CASUAL.ToString(),
                StartDate = DateTime.UtcNow.Date.AddDays(3),
                EndDate = DateTime.UtcNow.Date.AddDays(4),
                TotalDays = 2,
                Reason = "Family function and personal appointment",
                Status = LeaveStatus.PENDING.ToString(),
                CreatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new()
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000002"),
                UserId = st102Id,
                LeaveType = LeaveType.SICK.ToString(),
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(1),
                TotalDays = 1,
                Reason = "Medical checkup and consultation",
                Status = LeaveStatus.PENDING.ToString(),
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            }
        };
        await LeaveRequests.AddRangeAsync(seedLeaves);

        await SaveChangesAsync();
    }
}
