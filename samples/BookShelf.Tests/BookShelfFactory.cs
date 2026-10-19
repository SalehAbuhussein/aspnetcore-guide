using BookShelf.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookShelf.Tests;

public class BookShelfFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString = "Data Source=BookShelfTests;Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive = new(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

        // Let tests reach [Authorize] actions without signing in.
        builder.ConfigureTestServices(services =>
            services.AddAuthorization(options =>
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build()));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive.Dispose();
    }
}
