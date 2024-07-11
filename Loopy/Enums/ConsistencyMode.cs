namespace Loopy.Enums
{
    public enum ConsistencyMode
    {
        /// <summary>
        /// Eventual consistency
        /// </summary>
        Eventual = 0,

        /// <summary>
        /// FIFO consistency with all items
        /// </summary>
        Fifo = 1,

        /// <summary>
        /// FIFO consistency with only <see cref="Priority.P1"/> items and above
        /// </summary>
        FifoP1 = Fifo | (Priority.P1 << 4),

        /// <summary>
        /// FIFO consistency with only <see cref="Priority.P2"/> items and above
        /// </summary>
        FifoP2 = Fifo | (Priority.P2 << 4),

        /// <summary>
        /// FIFO consistency with only <see cref="Priority.P3"/> items and above
        /// </summary>
        FifoP3 = Fifo | (Priority.P3 << 4),
    }
}
