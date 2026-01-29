namespace DotnetTelemetryPlayground.Shared.DDD;

/// <summary>
/// Base class for entities.
/// </summary>
/// <typeparam name="T">The type of the entity's identifier.</typeparam>
public abstract class Entity<T> : IEntity<T>
{
    public T Id { get; set; } = default!;
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastModified { get; set; }
}

