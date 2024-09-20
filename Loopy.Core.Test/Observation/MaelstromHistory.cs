using Loopy.Core.Data;
using Loopy.Core.Enums;

namespace Loopy.Core.Test.Observation;

internal class MaelstromHistory : IHistoryReceiver
{
    private DateTime? _start;

    private readonly Map<ConsistencyMode, List<Entry>> _cmEntries = new();
    private readonly Dictionary<Type, Keyword> _entryTypeMapping = new()
    {
        { Type.Invoke, "invoke" },
        { Type.Ok, "ok" },
        { Type.Fail, "fail" },
        { Type.Info, "info" },
    };

    private readonly Keyword _readFunction = "read";
    private readonly Keyword _writeFunction = "write";

    void IHistoryReceiver.AddReadLog(Type type, int pid, ConsistencyMode cm, string key, int? value = null)
    {
        AddEntry(cm, _entryTypeMapping[type], pid, _readFunction, key, value);
    }

    void IHistoryReceiver.AddWriteLog(Type type, int pid, string key, int? value = null)
    {
        // write operations affect all consistency levels
        AddEntry(ConsistencyMode.Eventual, _entryTypeMapping[type], pid, _writeFunction, key, value);
        AddEntry(ConsistencyMode.Fifo, _entryTypeMapping[type], pid, _writeFunction, key, value);
    }

    private void AddEntry(ConsistencyMode cm, Keyword type, int process, Keyword function, string key, int? value = null)
    {
        lock (_cmEntries)
        {
            _start = _start ?? DateTime.UtcNow;
            var entries = _cmEntries[cm];
            entries.Add(new(
                entries.Count, (DateTime.UtcNow - _start.Value).Ticks,
                type, process, function, key, value));
        }
    }

    public bool HasEntries(ConsistencyMode cm) => _cmEntries.ContainsKey(cm);

    public void Save(ConsistencyMode cm, Stream target)
    {
        lock (_cmEntries)
        {
            if (!_cmEntries.ContainsKey(cm))
                return;

            using var writer = new StreamWriter(target);
            foreach (var entry in _cmEntries[cm])
                writer.WriteLine(entry.ToString());
        }
    }

    private record Entry(int Index, long Time, Keyword Type, int Process, Keyword Function, string Key, int? Value)
    {
        public override string ToString()
        {
            var functionValue = $"[\"{Key}\" {(Value.HasValue ? Value.Value.ToString() : "nil")}]";
            var pairs = new (Keyword kw, object v)[]
            {
                ("index", Index),
                ("time", Time),
                ("type", Type),
                ("process", Process),
                ("f", Function),
                ("value", functionValue)
            };

            return $"{{{string.Join(", ", pairs.Select(kv => $"{kv.kw} {kv.v}"))}}}";
        }
    }

    private record Keyword(string name)
    {
        public override string ToString() => $":{name}";

        public static implicit operator Keyword(string name) => new(name);
    }
}
