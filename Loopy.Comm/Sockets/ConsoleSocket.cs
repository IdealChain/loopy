using Loopy.Comm.Interfaces;
using System.Runtime.CompilerServices;

namespace Loopy.Comm.Sockets;

public class ConsoleSocket : IAsyncSocket<string>
{
    private static readonly SemaphoreSlim ConsoleReaderSemaphore = new(1);

    public async IAsyncEnumerable<string> ReceiveAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!await ConsoleReaderSemaphore.WaitAsync(TimeSpan.Zero, ct))
            throw new InvalidOperationException("Console socket does not support concurrent receive");

        try
        {
            // aquire standard input stream with own stream reader, as Console.In.ReadLineAsync() is, surprisingly, blocking
            using var stream = Console.OpenStandardInput();
            using var reader = new StreamReader(stream);

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null || ct.IsCancellationRequested)
                    break;

                yield return line;
            }
        }
        finally { ConsoleReaderSemaphore.Release(); }
    }

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        await Console.Out.WriteLineAsync(message);
    }
}
