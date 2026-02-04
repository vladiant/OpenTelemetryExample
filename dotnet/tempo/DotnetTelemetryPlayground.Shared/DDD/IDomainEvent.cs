using MediatR;

namespace DotnetTelemetryPlayground.Shared.DDD;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    Guid EventId => Guid.NewGuid();
    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTime OccurredOn => DateTime.Now;
    /// <summary>
    /// Type of the event.
    /// </summary>
    public string EventType => GetType().AssemblyQualifiedName ?? "GenericType";
}
