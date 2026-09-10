using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;
using PHF.Infrastructure.Services;
using PHF.Realtime.Hubs;
using PHF.Realtime.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers & SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// 2. Swagger with Bearer Auth
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PHF Work Management System API",
        Version = "v1",
        Description = "Enterprise Telemetry, Shift Attendance, Task Lifecycle & Realtime Workstation Feed API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 3. Database Provider (PostgreSQL or SQLite fallback, with auto-detection for Render DATABASE_URL)
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var pgConn = builder.Configuration.GetConnectionString("PostgreSQL");
var sqliteConn = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=phf_local.db";

var renderDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
               ?? builder.Configuration["DATABASE_URL"] 
               ?? builder.Configuration["SUPABASE_DATABASE_URL"]
               ?? builder.Configuration.GetConnectionString("PostgreSQL");

if (!string.IsNullOrWhiteSpace(renderDbUrl))
{
    try
    {
        dbProvider = "PostgreSQL";
        if (renderDbUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            renderDbUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(renderDbUrl);
            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            pgConn = $"Host={uri.Host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }
        else
        {
            pgConn = renderDbUrl;
        }
    }
    catch
    {
        // fallback to standard config
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pgConn))
    {
        options.UseNpgsql(pgConn);
    }
    else
    {
        options.UseSqlite(sqliteConn);
    }
});

// 4. JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") 
             ?? builder.Configuration["Jwt:Secret"] 
             ?? "PHF_Work_Management_Enterprise_Super_Secret_Key_2026_Secure_HMAC_SHA256_Auth!";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    // Allow SignalR connections to authenticate via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// 5. Rate Limiting for Authentication (protects against brute-force attacks)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 6. Hardened CORS for Web Portal and Local Windows Clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfigured", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            try
            {
                var uri = new Uri(origin);
                return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) 
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) 
                    || uri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// 6. Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddSingleton<IStorageService, LocalStorageService>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

var app = builder.Build();

// 7. Auto-migration / Database initialization & Seed Data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        try
        {
            _ = await dbContext.Users.AnyAsync();
        }
        catch
        {
            // Tables do not exist yet on target database (e.g. Supabase), generate and execute DDL script
            try
            {
                var script = dbContext.Database.GenerateCreateScript();
                await dbContext.Database.ExecuteSqlRawAsync(script);
            }
            catch { }
        }

        await dbContext.Database.EnsureCreatedAsync();

        if (dbContext.Database.IsSqlite())
        {
            // Migrate users table columns if needed (SQLite legacy support)
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE users ADD COLUMN username TEXT;");
            }
            catch { /* Column may already exist */ }

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE users ADD COLUMN work_mode TEXT DEFAULT 'IN_OFFICE';");
            }
            catch { /* Column may already exist */ }

            // Ensure leave_requests table exists if running on an existing SQLite database
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS leave_requests (
                        id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        leave_type TEXT NOT NULL,
                        start_date TEXT NOT NULL,
                        end_date TEXT NOT NULL,
                        total_days INTEGER NOT NULL DEFAULT 1,
                        reason TEXT NOT NULL,
                        status TEXT NOT NULL DEFAULT 'PENDING',
                        reviewed_by TEXT,
                        reviewed_at TEXT,
                        review_comments TEXT,
                        created_at TEXT NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                        FOREIGN KEY (reviewed_by) REFERENCES users(id) ON DELETE SET NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_leave_requests_user_status ON leave_requests (user_id, status);
                    CREATE INDEX IF NOT EXISTS ix_leave_requests_dates ON leave_requests (start_date, end_date);
                ");
            }
            catch { }
        }

        await dbContext.EnsureSeedDataAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 8. HTTP Pipeline
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "An unexpected server error occurred. Please try again later."
        });
    });
});

// Enforce standard security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PHF Work Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowConfigured");
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WorkstationHub>("/hubs/workstation");

// Status health endpoint under /api/health
app.MapGet("/api/health", () => Results.Ok(new
{
    Application = "PHF Work Management System API",
    Status = "Healthy",
    Version = "1.0.0",
    Swagger = "/swagger",
    Timestamp = DateTime.UtcNow
}));

// Fallback to Next.js Web Admin Portal
app.MapFallbackToFile("index.html");

app.Run();

