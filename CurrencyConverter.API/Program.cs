using Asp.Versioning;
using CurrencyConverter.API.Logging;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Application.Settings;
using CurrencyConverter.Application.Validators;
using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Data;
using CurrencyConverter.Infrastructure.Services;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/currency-converter-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers()
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<ConversionRequestDtoValidator>());

// Configure Currency Settings
builder.Services.Configure<CurrencySettings>(builder.Configuration.GetSection("CurrencySettings"));

// Add Memory Cache
builder.Services.AddMemoryCache();

// Register HttpContextAccessor for log enrichers
builder.Services.AddHttpContextAccessor();

// Register Caching Service
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Register HTTP Client for Frankfurter API and Currency Provider
builder.Services.AddHttpClient<FrankfurterApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.frankfurter.app/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "CurrencyConverter-API/1.0");
});
builder.Services.AddScoped<ICurrencyProvider, FrankfurterApiService>();

// Register Currency Provider Factory
builder.Services.AddScoped<ICurrencyProviderFactory, CurrencyProviderFactory>();

// Register Currency Service
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

// Configure Entity Framework with SQLite (skip if Testing environment)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Configure ASP.NET Identity (skip if Testing environment)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

        // Signin settings
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
}

// Configure JWT Authentication (skip if Testing environment)
if (!builder.Environment.IsEnvironment("Testing"))
{
    var jwtSettings = builder.Configuration.GetSection("JWT");
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Log JWT authentication events
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("JWT Token validated for user: {UserId}",
                    context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                return Task.CompletedTask;
            }
        };
    });
}

// Register JWT Token Service
builder.Services.AddScoped<IJwtTokenService, CurrencyConverter.Application.Services.JwtTokenService>();

// Configure API Versioning
builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("version"),
        new HeaderApiVersionReader("X-Version"),
        new MediaTypeApiVersionReader("ver")
    );
}).AddApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed-window", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromSeconds(10);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://localhost:3000") // Replace with your frontend URL
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("CurrencyConverter.API", "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.namespace"] = "CurrencyConverter",
                    ["service.instance.id"] = Environment.MachineName
                }))
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpRequest) =>
                {
                    // Don't trace health check endpoints to reduce noise
                    return !httpRequest.Request.Path.StartsWithSegments("/health");
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                // Add custom tags for external API calls
                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                {
                    if (httpRequestMessage.RequestUri?.Host == "api.frankfurter.app")
                    {
                        activity.SetTag("external.api", "frankfurter");
                        activity.SetTag("external.service", "exchange-rates");
                    }
                };
                options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                {
                    activity.SetTag("http.response.status_text", httpResponseMessage.ReasonPhrase);
                };
            })
            .AddSource("CurrencyConverter.Infrastructure.FrankfurterApi")
            .AddConsoleExporter(options =>
            {
                options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
            });
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "Currency Converter API v1");
    });
}

// Ensure database is created and seed initial data (skip if Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        // Apply migrations or create database
        await ApplyDatabaseMigrationsAsync(context, app.Environment);

        // Create default roles
        await SeedRolesAsync(roleManager);
    }
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

// Add Security Headers
app.UseHsts();
app.UseXContentTypeOptions();
app.UseXfo(options => options.Deny()); // Deny framing to prevent clickjacking
app.UseReferrerPolicy(options => options.NoReferrer()); // Control referrer information
app.UseCsp(options => options.DefaultSources(s => s.Self()).FrameAncestors(s => s.None())); // Content Security Policy

app.UseMiddleware<CurrencyConverter.API.Middleware.ExceptionHandlingMiddleware>();

app.UseCors("AllowSpecificOrigin");

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

try
{
    Log.Information("Starting Currency Converter API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Method to apply database migrations
static async Task ApplyDatabaseMigrationsAsync(ApplicationDbContext context, IWebHostEnvironment environment)
{
    try
    {
        // In Docker/Production environments, ensure data directory exists
        if (environment.IsProduction() || environment.IsEnvironment("Docker"))
        {
            var connectionString = context.Database.GetConnectionString();
            if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("/app/data/"))
            {
                var dataDirectory = Path.GetDirectoryName(connectionString.Replace("Data Source=", ""));
                if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                    Log.Information("Created data directory: {DataDirectory}", dataDirectory);
                }
            }
        }

        // Check if database exists
        var canConnect = await context.Database.CanConnectAsync();
        
        if (!canConnect)
        {
            Log.Information("Database does not exist, creating database...");
            await context.Database.EnsureCreatedAsync();
            Log.Information("Database created successfully");
        }
        else
        {
            Log.Information("Database connection established");
        }

        // Apply pending migrations if any (for future use with EF migrations)
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            Log.Information("Applying {Count} pending migrations", pendingMigrations.Count());
            await context.Database.MigrateAsync();
            Log.Information("Migrations applied successfully");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error occurred while applying database migrations");
        throw;
    }
}

// Method to seed default roles
static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
{
    var roles = new[] { "User", "Admin" };

    foreach (var roleName in roles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var role = new ApplicationRole
            {
                Name = roleName,
                Description = roleName == "Admin" ? "Administrator with full access" : "Regular user with basic access"
            };

            var result = await roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                Log.Information("Created role: {RoleName}", roleName);
            }
            else
            {
                Log.Error("Failed to create role {RoleName}: {Errors}", roleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

public partial class Program { }