namespace Loopy.Enums
{
    public enum ConsistencyMode
    {
        Eventual,
        Fifo,
        Causal,

        FifoAll = Fifo | (Priority.P0 << 4),
        FifoLow = Fifo | (Priority.P1 << 4),
        FifoNormal = Fifo | (Priority.P2 << 4),
        FifoHigh = Fifo | (Priority.P3 << 4),
    }
}
