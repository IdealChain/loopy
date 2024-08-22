using Loopy.Comm.Extensions;
using Loopy.Comm.Interfaces;
using NLog;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Loopy.Comm.Sockets;

public class JsonSocket<T>(IAsyncSocket<string> wrapped) : IAsyncSocket<T>
{
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(JsonSocket<T>));

    public async IAsyncEnumerable<T> ReceiveAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var line in wrapped.ReceiveAllAsync(ct))
        {
            if (string.IsNullOrEmpty(line))
                continue;

            T? msg;
            try { msg = Deserialize(line); }
            catch (Exception ex)
            {
                Logger.Fatal(ex, $"deserialization exception: {ex.Message}");
                throw;
            }

            if (msg != null)
                yield return msg;
        }
    }

    internal static T? Deserialize(string line)
    {
        // before deserialization, move type discriminator metadata field to the front
        // (still required for .NET 8, obsolete with .NET 9+)
        return JsonNode.Parse(line)
            .MoveMetadataToBeginning(x => x.Equals("type", StringComparison.Ordinal))
            .Deserialize<T>();
    }

    public async Task SendAsync(T message, CancellationToken ct = default)
    {
        await wrapped.SendAsync(Serialize(message), ct);
    }

    internal static string Serialize(T message) => JsonSerializer.Serialize(message);
}
