namespace Loopy.Data;

/// <summary>
/// "Safe" key-value dictionary that returns a default value for non-existing keys
/// </summary>
public class Map<TKey, TValue> : Dictionary<TKey, TValue>
    where TValue : new()
    where TKey : notnull
{
    private Lazy<bool> _storeCreatedValue = new(() => !typeof(TValue).IsValueType);

    public Map()
    {
    }

    public Map(IEnumerable<KeyValuePair<TKey, TValue>>? dict)
        : base(dict ?? Enumerable.Empty<KeyValuePair<TKey, TValue>>())
    {
    }

    public new TValue this[TKey key]
    {
        get
        {
            if (!TryGetValue(key, out var value))
            {
                value = new();

                if (_storeCreatedValue.Value)
                    base[key] = value;
            }

            return value;
        }
        set
        {
            if (value == null)
                Remove(key);
            else
                base[key] = value;
        }
    }
}