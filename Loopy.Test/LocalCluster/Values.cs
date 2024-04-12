using Loopy.Data;
using NUnit.Framework.Constraints;

namespace Loopy.Test.LocalCluster;

internal static class Values
{
    public static CollectionEquivalentConstraint EquivalentTo(params Value[] values) => Is.EquivalentTo(values);

    public static EqualConstraint EqualTo(params Value[] values) => Is.EqualTo(values);
}