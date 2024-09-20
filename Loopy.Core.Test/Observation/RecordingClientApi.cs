using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;

namespace Loopy.Core.Test.Observation;

/// <summary>
/// Wraps the client API and records invocation and completion entries for every operation
/// </summary>
internal class RecordingClientApi(IClientApi wrapped, IHistoryReceiver history, int pid) : IClientApi
{
    public int ReadQuorum
    {
        get => wrapped.ReadQuorum;
        set => wrapped.ReadQuorum = value;
    }

    public ConsistencyMode ConsistencyMode
    {
        get => wrapped.ConsistencyMode;
        set => wrapped.ConsistencyMode = value;
    }

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, CancellationToken cancellationToken = default)
    {
        history.AddReadLog(Type.Invoke, pid, ConsistencyMode, k.Name);
        var res = await wrapped.Get(k, cancellationToken);

        if (res.values.Length == 1 && int.TryParse(res.values[0].Data, out var value))
            history.AddReadLog(Type.Ok, pid, ConsistencyMode, k.Name, value);
        else
            history.AddReadLog(Type.Fail, pid, ConsistencyMode, k.Name);

        return res;
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        int? value = !v.IsEmpty && int.TryParse(v.Data, out var intValue) ? intValue : null;
        history.AddWriteLog(Type.Invoke, pid, k.Name, value);
        await wrapped.Put(k, v, cc, cancellationToken);
        history.AddWriteLog(Type.Ok, pid, k.Name, value);
    }

    public async Task Delete(Key k, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        history.AddWriteLog(Type.Invoke, pid, k.Name);
        await wrapped.Delete(k, cc, cancellationToken);
        history.AddWriteLog(Type.Ok, pid, k.Name);
    }
}
