using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Background;

public class CleanupJob(IServiceScopeFactory scopeFactory, ILogger<CleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-30);
                var deleted = await db.Todos
                    .Where(t => t.IsDone && t.CompletedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                logger.LogInformation("Cleanup removed {Count} todos", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cleanup failed");
            }
        }
    }
}
