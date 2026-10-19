using System.Security.Claims;
using System.Threading.RateLimiting;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseSqlite(builder.Configuration.GetConnectionString("Default"))
    .UseSeeding((context, _) =>
    {
        if (!context.Set<Book>().Any(b => b.Title == "Project Hail Mary"))
        {
            context.Set<Book>().Add(SampleData.Book());
            context.SaveChanges();
        }
    })
    .UseAsyncSeeding(async (context, _, ct) =>
    {
        if (!await context.Set<Book>().AnyAsync(b => b.Title == "Project Hail Mary", ct))
        {
            context.Set<Book>().Add(SampleData.Book());
            await context.SaveChangesAsync(ct);
        }
    }));

// Authentication and authorization
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanEditBooks", policy => policy.RequireRole("Admin", "Editor"))
    .AddPolicy("HasEmail", policy => policy.RequireClaim(ClaimTypes.Email));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageApplicationModelConvention(
        "Identity", "/Account/Login",
        model => model.EndpointMetadata.Add(new EnableRateLimitingAttribute("login")));
});

// Sessions
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Email
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailSender, LogEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}

// Background tasks
builder.Services.AddHostedService<UploadCleanupService>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("create", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsync("Too many requests. Try again in a minute.", ct);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Applies pending migrations, then runs UseAsyncSeeding (see the Seeding page).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.UseStaticFiles();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
