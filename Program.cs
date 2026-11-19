using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Data;
using MyMusic.Middleware;
using MyMusic.Models;
using MyMusic.Services;

var builder = WebApplication.CreateBuilder(args);
EnvironmentConfig.Apply(builder.Configuration);

// Render (and most PaaS) pass the port to listen on via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---------- Upload limits ----------
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Upload"));
var uploadOptions = builder.Configuration.GetSection("Upload").Get<UploadOptions>() ?? new UploadOptions();
var maxBody = (uploadOptions.MaxAudioMb + uploadOptions.MaxCoverMb + 2) * 1024L * 1024L;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxBody);
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxBody;
    o.ValueLengthLimit = 64 * 1024;
});

// ---------- Database ----------
var databaseUrl = builder.Configuration["DATABASE_URL"] ?? builder.Configuration["ConnectionStrings:Postgres"];
var usePostgres = !string.IsNullOrWhiteSpace(databaseUrl);

if (!usePostgres && builder.Environment.IsProduction() &&
    !builder.Configuration.GetValue<bool>("ALLOW_SQLITE_IN_PRODUCTION"))
{
    throw new InvalidOperationException(
        "DATABASE_URL is not set. Production needs PostgreSQL so data survives restarts. " +
        "(For a throwaway local test only: set ALLOW_SQLITE_IN_PRODUCTION=true.)");
}

var sqlitePath = Path.GetFullPath(
    builder.Configuration["Database:SqlitePath"] ?? "data/mymusic.db", builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (usePostgres)
        o.UseNpgsql(DatabaseUrl.ToConnectionString(databaseUrl!, builder.Configuration["DATABASE_SSL_MODE"] ?? "Prefer"));
    else
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
        o.UseSqlite($"Data Source={sqlitePath}");
    }
});

// ---------- Services ----------
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
if ((builder.Configuration["Storage:Provider"] ?? "Local").Equals("S3", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TrackService>();
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddScoped<LibraryService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var message = ctx.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Invalid request.";
            return new BadRequestObjectResult(new { error = message });
        };
    });

// ---------- Auth (HttpOnly cookie; works with <audio> and <img> tags) ----------
builder.Services.AddDataProtection()
    .SetApplicationName("MyMusic")
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "mymusic.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // https behind the Render proxy
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many attempts. Try again in a minute." }, ct);
    };
});

var app = builder.Build();

// X-Forwarded-* handling is enabled by ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (set in the Dockerfile).
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers["Cache-Control"] = "no-cache"
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ok" })
        : Results.StatusCode(503));

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.Json(new { error = "Not found." }, statusCode: 404));
app.MapFallbackToFile("index.html");

await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.Run();
