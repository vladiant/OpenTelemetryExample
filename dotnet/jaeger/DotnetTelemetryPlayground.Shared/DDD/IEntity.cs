namespace DotnetTelemetryPlayground.Shared.DDD;

/// <summary>
///  Marker interface for entities. 
/// </summary>
/// <typeparam name="T">The type of the entity's identifier.</typeparam>
public interface IEntity<T> : IEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public T Id { get; set; }
}

/// <summary>
/// Marker interface for entities.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Timestamp when the entity was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>
    /// Timestamp when the entity was last modified.
    /// </summary>
    public DateTime? LastModified { get; set; }
}
