namespace Loopy.Enums
{
    public enum ConsistencyMode
    {
        Eventual,
        Fifo,
        Causal,

        FifoAll = Fifo | (Priority.Bulk << 4),
        FifoLow = Fifo | (Priority.Low << 4),
        FifoNormal = Fifo | (Priority.Normal << 4),
        FifoHigh = Fifo | (Priority.High << 4),
    }
}
