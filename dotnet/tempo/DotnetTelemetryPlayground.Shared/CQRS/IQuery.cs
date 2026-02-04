using MediatR;

namespace DotnetTelemetryPlayground.Shared.CQRS;

/// <summary>
/// Marker interface for Queries
/// </summary>
/// <typeparam name="T">The type of the query result.</typeparam>
public interface IQuery<out T> : IRequest<T>
    where T : notnull
{
}
