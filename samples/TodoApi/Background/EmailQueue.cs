using System.Threading.Channels;

namespace TodoApi.Background;

public record EmailMessage(string To, string Subject, string Body);

public class EmailQueue
{
    private readonly Channel<EmailMessage> channel = Channel.CreateBounded<EmailMessage>(100);

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}
