using System.Diagnostics.CodeAnalysis;

namespace Loopy.Core.Data;

public readonly record struct Value(string? Data)
{
    [MemberNotNullWhen(false, nameof(Data))]
    public bool IsEmpty => Data == null;

    public static Value None => new(null);

    public static implicit operator Value(string? data) => new(data);

    public static implicit operator Value(int data) => new(Convert.ToString(data));

    public override string ToString() => Data ?? "-";
}
