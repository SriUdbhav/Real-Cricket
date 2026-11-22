namespace Cricket.Simulation.Core.Events
{
    /// <summary>
    /// Marker interface for all strongly-typed match simulation events.
    /// Implementations should be immutable readonly structs to minimize GC allocations.
    /// </summary>
    public interface IMatchEvent
    {
        long SequenceNumber { get; }
    }
}
