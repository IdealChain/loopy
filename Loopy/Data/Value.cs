namespace Loopy.Data;

public readonly record struct Value(string? Data)
{
    public bool IsEmpty => Data == null;

    public static Value None => new(null);

    public static implicit operator Value(string? data) => new(data);

    public static implicit operator Value(int data) => new(Convert.ToString(data));

    public override string ToString() => Data ?? "-";
}
