using System.Text.RegularExpressions;

namespace Loopy.Core.Data;

public readonly partial record struct NodeId(int Id)
{
    public static implicit operator NodeId(int id) => new(id);

    public static bool TryParse(string id, out NodeId nodeId)
    {
        var match = NodeIdRegex().Match(id);
        nodeId = match.Success ? int.Parse(match.Groups["id"].Value) : default;
        return match.Success;
    }

    public static NodeId Parse(string id)
    {
        if (!TryParse(id, out var nodeId))
            throw new ArgumentException($"not parseable: {id}", nameof(id));

        return nodeId;
    }

    [GeneratedRegex(@"^[nN]?(?<id>\d+)$")]
    private static partial Regex NodeIdRegex();

    public override string ToString() => $"N{Id}";
}
