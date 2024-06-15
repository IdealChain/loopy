using Loopy.Data;
using NUnit.Framework.Constraints;

namespace Loopy.Test;

internal static class Values
{
    public static CollectionEquivalentConstraint EquivalentTo(params Value[] values) => Is.EquivalentTo(values);

    public static EqualConstraint EqualTo(params Value[] values) => Is.EqualTo(values);

    public static EqualConstraint Empty(params Value[] values) => Is.Empty.Or.All.EqualTo(Value.None);
}
