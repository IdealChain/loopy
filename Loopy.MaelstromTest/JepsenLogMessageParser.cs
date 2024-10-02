using Loopy.Comm.Extensions;
using Loopy.Comm.MaelstromMessages;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Loopy.MaelstromTest;

public static class JepsenLogMessageParser
{
    public record Message(DateTimeOffset ts, Direction dir, Envelope? env);

    public enum Direction { send, recv };

    public static IEnumerable<Message> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = line.Split('\t');
            if (fields.Length < 2 ||
                !DateTimeOffset.TryParseExact(fields[0], "yyyy-MM-dd HH:mm:ss,fff'{GMT}'",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts))
                continue;

            var msgMatch = Regex.Match(fields[2], ":(?<dir>send|recv) #maelstrom.net.message.Message(?<msg>.+)");
            if (!msgMatch.Success ||
                !Enum.TryParse<Direction>(msgMatch.Groups["dir"].Value, out var dir) ||
                !EdnParser.TryParse(msgMatch.Groups["msg"].Value, out var msg))
                continue;

            var env = msg
                .MoveMetadataToBeginning(x => x.Equals("type", StringComparison.Ordinal))
                .Deserialize<Envelope>();

            yield return new Message(ts, dir, env);
        }
    }
}
