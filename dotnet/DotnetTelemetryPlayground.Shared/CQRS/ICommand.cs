using MediatR;

namespace DotnetTelemetryPlayground.Shared.CQRS;

/// <summary>
/// Marker interface for Commands with no return value
/// </summary>  
public interface ICommand : ICommand<Unit>
{
}

/// <summary>
/// Marker interface for Commands
/// </summary>
/// <typeparam name="TResponse">The type of the command result.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
