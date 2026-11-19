using AqarInsights.Api.Models;
using AqarInsights.Api.Services;
using AqarInsights.Api.Services.ForeignOwnership;
using AqarInsights.Api.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

var assembly = Assembly.GetExecutingAssembly();
var appVersion = assembly.GetName().Version?.ToString(3) ?? "0.0.0";
var appBuild = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? appVersion;

// Bootstrap logger captures failures during host configuration; it is replaced by the
// fully-configured logger once the host is built (see UseSerilog below).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

// Don't advertise the server implementation.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Levels/enrichers are config-driven (appsettings "Serilog"); the sink format is chosen by
// environment: human-readable text in Development, compact JSON everywhere else (for aggregators).
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services);

    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
    }
    else
    {
        configuration.WriteTo.Console(new CompactJsonFormatter());
        // Rolling daily JSON files for on-host retention (stdout stays the primary sink).
        // Directory is configurable (Logging:File:Directory) so it can point at a writable
        // location outside the read-only deployment folder; defaults to <app>/logs.
        var configuredLogDir = context.Configuration["Logging:File:Directory"];
        var logDir = string.IsNullOrWhiteSpace(configuredLogDir)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredLogDir), AppContext.BaseDirectory);
        configuration.WriteTo.File(
            new CompactJsonFormatter(),
            path: Path.Combine(logDir, "log-.json"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            rollOnFileSizeLimit: true,
            fileSizeLimitBytes: 50 * 1024 * 1024);
    }
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// CORS origins come from configuration (Cors:AllowedOrigins). Non-dev must configure them explicitly.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Cors:AllowedOrigins must be configured in non-Development environments.");
    allowedOrigins = ["http://localhost:4200"];
}
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              // Lets the SPA read report download names (they carry the city and period).
              .WithExposedHeaders("Content-Disposition");
    });
});

// Authentication & Authorization
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));
builder.Services.AddSingleton<IUserStore>(_ =>
    new JsonUserStore(DataPaths.UsersFile(builder.Configuration, builder.Environment)));
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenRevocationStore, InMemoryTokenRevocationStore>();

var jwtSecret = builder.Configuration["AuthSettings:JwtSecret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("AuthSettings:JwtSecret is required and must not be empty");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        // Reject tokens whose jti has been revoked by an explicit logout.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti))
                {
                    var store = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationStore>();
                    if (store.IsRevoked(jti))
                        context.Fail("Token has been revoked.");
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    // Fail closed: any endpoint without explicit [AllowAnonymous] requires authentication.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Throttle login attempts per client IP to slow brute-force.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 50, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Honour X-Forwarded-* from the ingress so the real client IP/scheme is used (rate limiting, logs).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Register domain services (paths resolved from configuration inside each extension).
builder.Services.AddForeignOwnershipServices(builder.Configuration, builder.Environment);
builder.Services.AddRentalControlsServices(builder.Configuration, builder.Environment);
builder.Services.AddReportingServices(builder.Configuration, builder.Environment);
builder.Services.AddAssistantServices(builder.Configuration);

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
foreach (var name in new[] { "Tajawal-Regular.ttf", "Tajawal-Bold.ttf" })
{
    using var fontStream = typeof(Program).Assembly
        .GetManifestResourceStream($"AqarInsights.Api.Resources.Fonts.{name}")
        ?? throw new InvalidOperationException($"Embedded font resource not found: {name}");
    QuestPDF.Drawing.FontManager.RegisterFont(fontStream);
}

var app = builder.Build();

// Handle --import CLI argument
var importArg = args.FirstOrDefault(a => a == "--import");
if (importArg != null)
{
    var converter = app.Services.GetRequiredService<IExcelConverterService>();
    var report = await converter.ConvertLatestAsync();
    if (!report.Success)
    {
        Console.WriteLine($"Import failed: {string.Join(", ", report.Errors)}");
        return;
    }
    Console.WriteLine($"Import successful: {report.ValidRows} records from {report.TotalRows} rows");
    foreach (var w in report.Warnings)
        Console.WriteLine($"  warning [{w.Sheet} r{w.Row}]: {w.Issue}");
}

if (args.Any(a => a == "--import-controls"))
{
    var rc = app.Services.GetRequiredService<AqarInsights.Api.Services.RentalControls.IRentalControlsConverterService>();
    var rcReport = await rc.ConvertAllAsync();
    Console.WriteLine(rcReport.Success
        ? $"Rental-controls import successful: {rcReport.ValidRows} cities → {string.Join(", ", rcReport.FilesGenerated)}"
        : $"Rental-controls import failed: {string.Join(", ", rcReport.Errors)}");
    foreach (var w in rcReport.Warnings)
        Console.WriteLine($"  warning [{w.Sheet}]: {w.Issue}");
    return;
}

// --apply-monthly-update --from <updatesDir> --month yyyy-MM [--steps cpi,rental-vacancy,public-vacancy,media,gap]
// Patches the source workbooks in Data/rental-controls-source with a month's update files; --import-controls publishes.
if (args.Any(a => a == "--apply-monthly-update"))
{
    string? Arg(string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    if (Arg("--from") is not { } updatesDir
        || !AqarInsights.Api.Services.RentalControls.Snapshots.YearMonth.TryParse(Arg("--month"), out var updateMonth))
    {
        Console.WriteLine("Monthly update failed: --from <updatesDir> and --month yyyy-MM are required.");
        return;
    }
    var steps = Arg("--steps")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        ?? AqarInsights.Api.Services.RentalControls.MonthlyUpdate.MonthlyUpdateRunner.AllSteps.ToArray();
    var sourceDir = AqarInsights.Api.Extensions.DataPaths.RentalControlsSourceDir(builder.Configuration, builder.Environment);
    try
    {
        var update = new AqarInsights.Api.Services.RentalControls.MonthlyUpdate.MonthlyUpdateRunner(
            sourceDir, Path.GetFullPath(updatesDir), updateMonth).Run(steps);
        foreach (var c in update.Changes) Console.WriteLine($"  change: {c}");
        foreach (var w in update.Warnings) Console.WriteLine($"  warning: {w}");
        Console.WriteLine($"Monthly update {updateMonth.Id}: {update.Changes.Count} changes, {update.Warnings.Count} warnings. Review them, then run --import-controls.");
    }
    // RentalControlsReadException: a missing sheet or renamed header in an update workbook (SheetReader);
    // ArgumentException/KeyNotFoundException: a malformed media workbook (sheet or column lookup).
    catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException
        or AqarInsights.Api.Services.RentalControls.Reading.RentalControlsReadException
        or ArgumentException or KeyNotFoundException)
    {
        Console.WriteLine($"Monthly update failed (workbooks may be partly patched; restore them with git): {ex.Message}");
    }
    return;
}

// --migrate-controls-storage: one-time move to quarter-only releases + monthly_movement + corrections files.
if (args.Any(a => a == "--migrate-controls-storage"))
{
    var migration = new AqarInsights.Api.Services.RentalControls.Tables.ReleaseStorageMigration(
        app.Services.GetRequiredService<AqarInsights.Api.Services.RentalControls.Tables.IRentalControlsRepository>(),
        app.Services.GetRequiredService<ILogger<AqarInsights.Api.Services.RentalControls.Tables.ReleaseStorageMigration>>());
    foreach (var line in await migration.MigrateAsync())
        Console.WriteLine($"  {line}");
    return;
}

// --snapshot-controls --period yyyy-Qn [--from <releaseId>] [--source <workbooksDir>] [--commit <sha>] [--force]
// Freezes a release (default: live) as the quarter's release, kept as is. An existing quarter is only rewritten with
// --force (its corrections file is kept). Monthly releases are no longer written.
if (args.Any(a => a == "--snapshot-controls"))
{
    string? Arg(string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    var repository = app.Services.GetRequiredService<AqarInsights.Api.Services.RentalControls.Tables.IRentalControlsRepository>();
    var releases = app.Services.GetRequiredService<AqarInsights.Api.Services.RentalControls.Tables.ReleaseWriter>();
    var from = Arg("--from") ?? AqarInsights.Api.Models.RentalControls.Tables.ReleaseIds.Live;
    try
    {
        if (await repository.LoadReleaseAsync(from) is not { } rows)
        {
            Console.WriteLine($"Snapshot skipped: no rental-controls release '{from}' at {repository.Location(from)}");
            return;
        }
        if (Arg("--period") is not { } period)
        {
            Console.WriteLine("Snapshot failed: --period yyyy-Qn is required (monthly releases are no longer written).");
            return;
        }
        if (repository.GetReleases().Any(r => r.Id == period) && !args.Contains("--force"))
        {
            Console.WriteLine($"Snapshot refused: release {period} exists (add --force to rewrite it; its corrections are kept).");
            return;
        }
        var data = AqarInsights.Api.Services.RentalControls.Tables.ReleaseMapper.FromRows(rows);
        var source = Arg("--source") is { } sourceArg ? Path.GetFullPath(sourceArg) : null;
        if (await releases.WriteAsync(data, period, source, Arg("--commit")) is { } release)
            Console.WriteLine($"Snapshot written: {release.Id} ({release.Label}) → {repository.Location(release.Id)}");
        else
            Console.WriteLine($"Snapshot skipped: no rental-controls data in release '{from}'");
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException)
    {
        // e.g. --from with a folder path (the pre-tables syntax) instead of a release id, or a period in the future.
        Console.WriteLine($"Snapshot failed: {ex.Message}");
    }
    return;
}

// Handle --normalize-data CLI argument
if (args.Any(a => a == "--normalize-data"))
{
    var transactionsFile = AqarInsights.Api.Extensions.DataPaths.TransactionsFile(builder.Configuration, builder.Environment);
    var dataDir = Path.GetDirectoryName(transactionsFile) ?? "Data";
    await AqarInsights.Api.Tools.DataNormalizationScript.RunAsync(dataDir);
    return;
}

// Load data and compute aggregations at startup
var loader = app.Services.GetRequiredService<IDataLoaderService>();
await loader.LoadAsync();

var rcStore = app.Services.GetRequiredService<AqarInsights.Api.Services.RentalControls.Store.IRentalControlsStore>();
await rcStore.LoadAsync();
var rcAggregation = app.Services.GetRequiredService<AqarInsights.Api.Services.RentalControls.IRentalControlsAggregationService>();
rcAggregation.Recompute();

var aggregation = app.Services.GetRequiredService<IAggregationService>();
aggregation.Recompute();

var filterOptions = app.Services.GetRequiredService<IFilterOptionsService>();
await filterOptions.LoadAsync();

app.UseForwardedHeaders();

// One structured log line per request; health-check probes are demoted to Verbose to avoid noise.
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
        ex is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
        : httpContext.Request.Path.StartsWithSegments("/v1/api/health-check") ? LogEventLevel.Verbose
        : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnostic, httpContext) =>
    {
        diagnostic.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        diagnostic.Set("Host", httpContext.Request.Host.Value ?? "");
        diagnostic.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

app.UseExceptionHandler();

var isDevelopment = app.Environment.IsDevelopment();
app.Use(async (ctx, next) =>
{
    var headers = ctx.Response.Headers;
    // Don't advertise the underlying stack. Kestrel's Server header is already suppressed
    // (AddServerHeader = false); strip here too in case a host/proxy re-adds them.
    headers.Remove("Server");
    headers.Remove("X-Powered-By");
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers["X-XSS-Protection"] = "0";
    headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=(), usb=()";
    if (!isDevelopment)
    {
        // JSON-only API: lock down to nothing and enforce HTTPS. (Skipped in dev so the Scalar UI loads.)
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }
    await next();
});

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness/readiness probe endpoint expected by the Helm chart
app.MapGet("/v1/api/health-check", () => Results.Ok(new { status = "healthy", version = appVersion, build = appBuild }))
   .AllowAnonymous();

app.MapControllers();
await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly during startup");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
