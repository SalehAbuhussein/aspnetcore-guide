using System.Threading.Channels;

namespace BookShelf.Services;

public record EmailMessage(string To, string Subject, string Body);

public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public ValueTask EnqueueAsync(EmailMessage message) =>
        _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
