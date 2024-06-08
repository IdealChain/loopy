using Loopy.Data;
using Loopy.Enums;
using Object = Loopy.Data.Object;

namespace Loopy.Interfaces;

internal interface IConsistencyStore
{
    void CheckMerge(Key k, Object o);
    
    Object Fetch(Key k, Priority p = default);
}
