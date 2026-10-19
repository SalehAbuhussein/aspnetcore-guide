namespace BookShelf.Services;

public class EmailWorker(
    EmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendAsync(message.To, message.Subject, message.Body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send email to {To}", message.To);
            }
        }
    }
}
