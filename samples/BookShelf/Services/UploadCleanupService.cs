using BookShelf.Data;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public class UploadCleanupService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<UploadCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanUpAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Upload cleanup failed");
            }
        }
    }

    private async Task CleanUpAsync(CancellationToken ct)
    {
        var folder = Path.Combine(env.WebRootPath, "uploads");
        if (!Directory.Exists(folder)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inUse = await db.Books
            .Where(b => b.CoverPath != null)
            .Select(b => b.CoverPath!)
            .ToListAsync(ct);
        var inUseNames = inUse.Select(p => Path.GetFileName(p)).ToHashSet();

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var old = File.GetCreationTimeUtc(file) < DateTime.UtcNow.AddDays(-1);
            if (old && !inUseNames.Contains(Path.GetFileName(file)))
            {
                File.Delete(file);
                logger.LogInformation("Deleted unused upload {File}", file);
            }
        }
    }
}
