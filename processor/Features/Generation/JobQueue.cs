using System.Threading.Channels;

namespace EzCert.Processor.Features.Generation;

// Wake-up signal only — ProcessingJobs in PostgreSQL is the durable queue
// (source of truth). The worker polls the DB; this channel just avoids an
// idle 2s polling delay when a new job lands.
public sealed class JobQueue
{
    private readonly Channel<Guid> _wake = Channel.CreateUnbounded<Guid>();

    public void Wake(Guid jobId) => _wake.Writer.TryWrite(jobId);

    // Returns when a job was signalled, or null after the timeout.
    public async Task<Guid?> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return await _wake.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
