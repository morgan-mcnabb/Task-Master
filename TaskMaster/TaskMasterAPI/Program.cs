using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Domain.Common.Abstract;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskMasterApi.Services;
using Domain.Abstract.Persistence;
using Domain.Abstract.Repositories;
using Infrastructure.Repositories;
using TaskMasterApi.Mapping;
using FluentValidation;
using TaskMasterApi.Validation;
using Application.Abstract.Services;
using Application.Services;
using TaskMasterApi.Middleware;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// Configuration
// ----------------------------------------------------------------------------

const string corsPolicyName = "SpaDevCorsPolicy";
var spaDevOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173";

// ----------------------------------------------------------------------------
// Logging
// ----------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

// ----------------------------------------------------------------------------
// MVC + JSON + Validation
// ----------------------------------------------------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<FluentValidationActionFilter>();
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddValidatorsFromAssembly(typeof(TaskMasterApi.Validation.Tasks.CreateTaskRequestValidator).Assembly);

// ----------------------------------------------------------------------------
// Swagger (dev convenience)
// ----------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "TaskMaster API",
        Version = "v1",
        Description = "Task management MVP"
    });
});

// ----------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();


// ----------------------------------------------------------------------------
// DI: correlation id, repositories, services
// ----------------------------------------------------------------------------
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();
builder.Services.AddScoped<CorrelationIdMiddleware>();
builder.Services.AddScoped<RequestLoggingMiddleware>();

builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<ITagRepository, EfTagRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddSingleton<ITaskMapper, TaskMapper>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<SecurityHeadersMiddleware>();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<SecurityHeadersOptions>();
// ----------------------------------------------------------------------------
// EF Core: SQLite
// ----------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=taskmaster.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "dp-keys")))
    .SetApplicationName("TaskMaster");

// ----------------------------------------------------------------------------
// Identity + Cookie auth
// ----------------------------------------------------------------------------
builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = false;

        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;

        // Lockout: thwart brute-force attempts
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 10;           
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "taskmaster.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax; // same-origin SPA in prod
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden; 
                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// ----------------------------------------------------------------------------
// Reverse proxy support (X-Forwarded-*). Important when hosting behind Nginx/Apache.
// ----------------------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ----------------------------------------------------------------------------
// CORS: enabled only for local DEV when SPA runs on a different origin.
// In production (same-origin), we do not register the middleware.
// ----------------------------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(corsPolicyName, policy =>
        {
            policy.WithOrigins(spaDevOrigin)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}

// ----------------------------------------------------------------------------
// Rate limiting
// ----------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
        var userIdOrIp = isAuthenticated
            ? httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";

        var partitionKey = isAuthenticated
            ? $"user:{userIdOrIp}"
            : $"ip:{(userIdOrIp ?? "unknown").Replace(":", "_")}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// ----------------------------------------------------------------------------
// Database initialization: apply migrations if present (preferred), else EnsureCreated for MVP.
// ----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var bootstrapLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                              .CreateLogger("DbInitializer");
    try
    {
        var hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            dbContext.Database.Migrate();
            bootstrapLogger.LogInformation("Applied EF Core migrations to SQLite.");
        }
        else
        {
            dbContext.Database.EnsureCreated();
            bootstrapLogger.LogInformation("Created SQLite schema via EnsureCreated (no migrations found).");
        }
    }
    catch (Exception ex)
    {
        bootstrapLogger.LogError(ex, "Database initialization failed.");
        throw;
    }
}

// ----------------------------------------------------------------------------
// HTTPS/HSTS: force HTTPS in non-development.
// ----------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ----------------------------------------------------------------------------
// Forwarded headers (must be early in pipeline, before auth/redirects).
// ----------------------------------------------------------------------------
app.UseForwardedHeaders();
app.UseSecurityHeaders();

// ----------------------------------------------------------------------------
// Correlation Id + error handling + dev swagger
// ----------------------------------------------------------------------------
app.UseCorrelationId();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ----------------------------------------------------------------------------
// CORS: only for dev (separate Vite server). In production (same-origin) we do not call UseCors.
// ----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseCors(corsPolicyName);
}

// ----------------------------------------------------------------------------
// Rate limiting + auth
// ----------------------------------------------------------------------------
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ----------------------------------------------------------------------------
// Request logging (one line per request)
// ----------------------------------------------------------------------------
app.UseRequestLogging();

// ----------------------------------------------------------------------------
// Static files + SPA fallback (same origin)
// Serve the built SPA from wwwroot and fall back to index.html for client routing.
// ----------------------------------------------------------------------------
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});
app.UseStaticFiles();

// ----------------------------------------------------------------------------
// API controllers
// ----------------------------------------------------------------------------
app.MapControllers();

// ----------------------------------------------------------------------------
// SPA fallback: any non-file path not matched above goes to index.html
// (lets the client router handle /tasks/123, etc.)
// ----------------------------------------------------------------------------
app.MapFallbackToFile("index.html");

app.Run();
