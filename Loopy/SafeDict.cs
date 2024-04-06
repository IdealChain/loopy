namespace Loopy;

public class SafeDict<TKey, TValue> : Dictionary<TKey, TValue>
    where TValue : new()
    where TKey : notnull
{
    private Lazy<bool> _storeCreatedValue = new(() => !typeof(TValue).IsValueType);
    
    public SafeDict()
    { }

    public SafeDict(IDictionary<TKey, TValue> dict) : base(dict)
    { }

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