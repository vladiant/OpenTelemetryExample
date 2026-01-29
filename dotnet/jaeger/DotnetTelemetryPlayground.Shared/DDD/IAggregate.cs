namespace DotnetTelemetryPlayground.Shared.DDD;

/// <summary>
/// Marker interface for aggregates.
/// </summary>
/// <typeparam name="T">The type of the aggregate's identifier.</typeparam>
public interface IAggregate<T> : IAggregate, IEntity<T>
{
}

/// <summary>
/// Marker interface for aggregates.
/// </summary>
public interface IAggregate : IEntity
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    IDomainEvent[] ClearDomainEvents();
}

