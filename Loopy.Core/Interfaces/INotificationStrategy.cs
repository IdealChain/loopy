using Loopy.Core.Data;
using Loopy.Core.Enums;

namespace Loopy.Core.Interfaces;

public interface INotificationStrategy
{
    void NotifyValueChanged(Key key, ConsistencyMode cm, Value[] values, CausalContext cc);
}
