using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DotnetTelemetryPlayground.ApiService.Shared.Infrastructure.Db;

// Interceptor to dispatch domain events before and after saving changes
public class DispatchDomainEvents(IMediator mediator) : SaveChangesInterceptor
{
    private List<IDomainEvent> _domainEvents = [];
    private List<IAggregate>? _aggregates;


    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        DoDispatchDomainEvents(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await DoDispatchDomainEvents(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Clear the domain events after they have been processed
        ClearDomainEvents();

        // Check if the context is a WeatherDbContext and if it needs to signal the outbox
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        // Clear the domain events after they have been processed
        ClearDomainEvents();

        // Check if the context is a WeatherDbContext and if it needs to signal the outbox
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DoDispatchDomainEvents(DbContext? context)
    {
        if (context == null) return;

        _aggregates = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(a => a.Entity.DomainEvents.Any())
            .Select(a => a.Entity)
            .ToList();

        _domainEvents = _aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (var domainEvent in _domainEvents)
            await mediator.Publish(domainEvent);
    }

    private void ClearDomainEvents()
    {
        _domainEvents.Clear();
        _aggregates?.ForEach(a => a.ClearDomainEvents());
        _aggregates = null;
    }
}
