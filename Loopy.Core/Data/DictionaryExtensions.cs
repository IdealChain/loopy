namespace Loopy.Core.Data;

public static class DictionaryExtensions
{
    public static void MergeIn<TK, TV>(this Dictionary<TK, TV> dict,
        IEnumerable<KeyValuePair<TK, TV>> pairs,
        Func<TV, TV, TV>? conflictResolver = null) where TK : notnull
    {
        MergeIn(dict, pairs.Select(kv => (kv.Key, kv.Value)), conflictResolver);
    }

    public static void MergeIn<TK, TV>(this Dictionary<TK, TV> dict,
        IEnumerable<(TK, TV)> tuples,
        Func<TV, TV, TV>? conflictResolver = null) where TK : notnull
    {
        foreach (var (k, v) in tuples)
        {
            if (dict.TryGetValue(k, out var existing) && !Equals(v, existing))
            {
                if (conflictResolver == null)
                    throw new InvalidOperationException("Conflict: resolver required");

                dict[k] = conflictResolver(existing, v);
            }
            else
            {
                dict[k] = v;
            }
        }
    }

    public static string AsCsv<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable.TryGetNonEnumeratedCount(out var count) && count == 0)
            return "-";

        return string.Join(", ", enumerable);
    }

    public static string AsCsv<T>(this IEnumerable<T> enumerable, Func<T, string> formatter)
    {
        if (enumerable.TryGetNonEnumeratedCount(out var count) && count == 0)
            return "-";

        return string.Join(", ", enumerable.Select(formatter));
    }
}
