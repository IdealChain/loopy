namespace Loopy.Data;

public readonly record struct Key(string Name)
{
    public static implicit operator Key(string name) => new(name);

    public override string ToString() => Name;
}