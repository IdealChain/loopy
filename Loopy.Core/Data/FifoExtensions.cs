using Loopy.Core.Enums;
using System.Diagnostics;

namespace Loopy.Core.Data;

public static class FifoExtensions
{
    internal static readonly Priority[] Priorities = Enum.GetValues<Priority>().ToArray();

    /// <summary>
    /// Gets the update ID of the predecessor update for the given priority
    /// </summary>
    public static int GetFifoPredecessor(this int[] fifoDistances, int updateId, Priority prio)
    {
        Trace.Assert(fifoDistances.Length == Priorities.Length, "FIFO priority/distance length mismatch");

        var dist = fifoDistances[(int)prio];
        Trace.Assert(updateId >= dist && dist >= 1, "invalid updateId or distance");
        return updateId - dist;
    }

    /// <summary>
    /// Gets the update ID of the predecessor update for the given priority
    /// </summary>
    public static int GetFifoPredecessor(this KeyValuePair<Dot, (Value value, int[] fifoDistances)> dotValue, Priority prio)
    {
        return GetFifoPredecessor(dotValue.Value.fifoDistances, dotValue.Key.UpdateId, prio);
    }

    /// <summary>
    /// Gets the range of update IDs this update can FIFO skip for the given priority
    /// </summary>
    public static IEnumerable<int> GetFifoSkippableUpdates(this int[] fifoDistances, int updateId, Priority prio)
    {
        for (int i = GetFifoPredecessor(fifoDistances, updateId, prio) + 1; i < updateId; i++)
            yield return i;
    }

    /// <summary>
    /// Gets the range of update IDs this update can FIFO skip for the given priority
    /// </summary>
    public static IEnumerable<int> GetFifoSkippableUpdates(this KeyValuePair<Dot, (Value value, int[] fifoDistances)> dotValue, Priority prio)
    {
        return GetFifoSkippableUpdates(dotValue.Value.fifoDistances, dotValue.Key.UpdateId, prio);
    }

    /// <summary>
    /// Strips redundant entries from the returned FIFO priority/distances map
    /// </summary>
    public static Map<Priority, int> StripFifoDistances(this int[] fifoDistances)
    {
        Trace.Assert(fifoDistances.Length == Priorities.Length, "FIFO priority/distance length mismatch");

        var currentDistance = 1;
        var map = new Map<Priority, int>();

        // for equal distances, keep only first (lowest-prio) entry
        for (var i = 0; i < Priorities.Length && i < fifoDistances.Length; i++)
        {
            if (fifoDistances[i] > currentDistance)
                map[Priorities[i]] = currentDistance = fifoDistances[i];
        }

        return map;
    }

    /// <summary>
    /// Fills back omitted entries to rebuild the complete FIFO priority/distances vector
    /// </summary>
    public static int[] FillFifoDistances(this IReadOnlyDictionary<Priority, int> fifoDistanceMap)
    {
        var currentDistance = 1;
        var fd = new int[Priorities.Length];

        for (var i = 0; i < fd.Length; i++)
        {
            if (fifoDistanceMap.TryGetValue(Priorities[i], out var d) && d > currentDistance)
                currentDistance = d;

            fd[i] = currentDistance;
        }

        return fd;
    }
}
