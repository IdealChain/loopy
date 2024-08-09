using Loopy.Core.Data;
using Loopy.Core.Interfaces;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Loopy.Test;

internal static class Values
{
    public static CollectionEquivalentConstraint EquivalentTo(params Value[] values) => Is.EquivalentTo(values);

    public static EqualConstraint EqualTo(params Value[] values) => Is.EqualTo(values);

    public static EqualConstraint Empty(params Value[] values) => Is.Empty.Or.All.EqualTo(Value.None);

    public static async Task<Value[]> GetValues(this IClientApi api, Key k)
    {
        var (values, _) = await api.Get(k);
        return values;
    }

    public static async Task<CausalContext> GetCC(this IClientApi api, Key k)
    {
        var (_, cc) = await api.Get(k);
        return cc;
    }
}
