using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Scratchdeck.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\Scratchdeck.SingleInstance";
    private readonly string _pipeName = $"Scratchdeck.Activation.{Process.GetCurrentProcess().SessionId}";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listenerTask;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public bool IsFirstInstance { get; }

    public event EventHandler? ActivationRequested;

    public void StartListening()
    {
        if (!IsFirstInstance || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = ListenAsync(_shutdown.Token);
    }

    public async Task NotifyFirstInstanceAsync()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);
            await client.WriteAsync(Encoding.UTF8.GetBytes("ACTIVATE"), timeout.Token);
            await client.FlushAsync(timeout.Token);
        }
        catch
        {
            // The first instance may still be starting or shutting down.
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);
                var buffer = new byte[32];
                var count = await server.ReadAsync(buffer, cancellationToken);
                var message = Encoding.UTF8.GetString(buffer, 0, count);
                if (message.StartsWith("ACTIVATE", StringComparison.Ordinal))
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
