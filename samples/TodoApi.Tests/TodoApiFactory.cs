using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TodoApi.Data;

namespace TodoApi.Tests;

public class TodoApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString = "Data Source=TodoTests;Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive = new(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive.Dispose();
    }
}
