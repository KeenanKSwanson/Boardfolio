using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Boardfolio.Api.Data;
using Boardfolio.Api.DataAccessors;
using Boardfolio.Api.Models;
using Boardfolio.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var connection = builder.Configuration.GetConnectionString("Boardfolio");
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("ConnectionStrings:Boardfolio is required for SQL Server.");
    builder.Services.AddDbContext<SqlServerBoardfolioDbContext>(options => options.UseSqlServer(connection));
    builder.Services.AddScoped<BoardfolioDbContext>(services => services.GetRequiredService<SqlServerBoardfolioDbContext>());
}
else if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<BoardfolioDbContext>(options => options.UseSqlite(
        builder.Configuration.GetConnectionString("Boardfolio") ?? "Data Source=App_Data/boardfolio.db"));
}
else
{
    throw new InvalidOperationException("Database:Provider must be Sqlite or SqlServer.");
}
builder.Services.AddScoped<KeyboardBuildDataAccessor>();
builder.Services.AddScoped<KeyboardBuildService>();
builder.Services.AddAuthorization(options => options.AddPolicy("Account", policy => policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme).RequireAuthenticatedUser()));
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<BoardfolioDbContext>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (TrackingException error)
    {
        await Results.Problem(statusCode: error.Status, title: error.Message).ExecuteAsync(context);
    }
});
if (app.Environment.IsDevelopment())
{
    // Only the local SQLite database is migrated on startup. Azure SQL uses a deployment step.
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoardfolioDbContext>();
        await db.Database.MigrateAsync();
    }
}
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("ApiDocumentation:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options =>
    {
        options.WithTitle("Boardfolio API");
        if (!app.Environment.IsDevelopment())
            options.HideTestRequestButton();
    });
}
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGroup("/api/auth").WithTags("Authentication").MapIdentityApi<ApplicationUser>().RequireRateLimiting("accounts");
app.MapControllers();
app.Run();

public partial class Program;

