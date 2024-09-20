using Loopy.Core.Enums;

namespace Loopy.Core.Test.Observation
{
    internal interface IHistoryReceiver
    {
        void AddReadLog(Type type, int pid, ConsistencyMode cm, string key, int? value = null);
        void AddWriteLog(Type type, int pid, string key, int? value = null);
    }

    internal enum Type
    {
        Invoke,
        Ok,
        Fail,
        Info,
    }
}
