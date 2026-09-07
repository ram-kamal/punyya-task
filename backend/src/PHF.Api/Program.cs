using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// 3. Database Provider (PostgreSQL or SQLite fallback)
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var pgConn = builder.Configuration.GetConnectionString("PostgreSQL");
var sqliteConn = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=phf_local.db";

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
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "PHF_Work_Management_Enterprise_Super_Secret_Key_2026_Secure_HMAC_SHA256_Auth!";
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

// 5. CORS for Web Portal and Local Windows Clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 6. Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddSingleton<IStorageService, LocalStorageService>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

var app = builder.Build();

// 7. Auto-migration / Database initialization & Seed Data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.EnsureSeedDataAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 8. HTTP Pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PHF Work Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WorkstationHub>("/hubs/workstation");

// Root status endpoint
app.MapGet("/", () => Results.Ok(new
{
    Application = "PHF Work Management System API",
    Status = "Healthy",
    Version = "1.0.0",
    Swagger = "/swagger",
    Timestamp = DateTime.UtcNow
}));

app.Run();
