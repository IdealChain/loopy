using Loopy.Core.Enums;
using System.Text.RegularExpressions;

namespace Loopy.Core.Data;

public readonly partial record struct Key
{
    public Key(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));

        var m = KeyPriorityRegex().Match(name);
        if (m.Success)
            Priority = (Priority)int.Parse(m.Groups["prio"].Value);
    }

    public string Name { get; }

    public Priority Priority { get; } = Priority.P0;


    public static implicit operator Key(string name) => new(name);

    public override string ToString() => Name;

    [GeneratedRegex("^P(?<prio>[0-3])", RegexOptions.IgnoreCase)]
    private static partial Regex KeyPriorityRegex();
}
