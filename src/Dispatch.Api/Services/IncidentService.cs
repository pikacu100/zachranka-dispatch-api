using Dispatch.Api.Caching;
using Dispatch.Api.Contracts;
using Dispatch.Api.Data;
using Dispatch.Api.Domain;
using Dispatch.Api.Errors;
using Dispatch.Api.Hubs;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Services;

public class IncidentService(
    DispatchDbContext db,
    IDispatchNotifier notifier,
    DispatchCache cache,
    TimeProvider clock
)
{
    // Allowed manual status changes. New -> Dispatched happens by assigning a unit.
    private static readonly Dictionary<IncidentStatus, IncidentStatus[]> AllowedTransitions = new()
    {
        [IncidentStatus.New] = [IncidentStatus.Closed],
        [IncidentStatus.Dispatched] = [IncidentStatus.OnScene, IncidentStatus.Closed],
        [IncidentStatus.OnScene] = [IncidentStatus.Closed],
        [IncidentStatus.Closed] = [],
    };

    public async Task<IReadOnlyList<IncidentResponse>> GetAllAsync(
        IncidentStatus? status,
        IncidentPriority? priority,
        CancellationToken ct
    )
    {
        var query = IncidentsWithAssignments();

        if (status is not null)
            query = query.Where(i => i.Status == status);

        if (priority is not null)
            query = query.Where(i => i.Priority == priority);

        var incidents = await query.OrderByDescending(i => i.Id).ToListAsync(ct);

        return [.. incidents.Select(i => i.ToResponse())];
    }

    public async Task<IncidentResponse> GetAsync(int id, CancellationToken ct) =>
        (await LoadAsync(id, ct)).ToResponse();

    public async Task<IncidentResponse> CreateAsync(
        CreateIncidentRequest request,
        CancellationToken ct
    )
    {
        var incident = new Incident
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Address = request.Address.Trim(),
            Priority = request.Priority,
            CreatedAt = clock.GetUtcNow(),
        };

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(ct);

        // Evict before notifying, so clients that refetch on the SignalR event get fresh data.
        await cache.InvalidateIncidentsAsync(ct);
        var response = incident.ToResponse();
        await notifier.IncidentCreatedAsync(response);
        return response;
    }

    public async Task<IncidentResponse> AssignUnitAsync(
        int incidentId,
        int unitId,
        CancellationToken ct
    )
    {
        var incident = await LoadAsync(incidentId, ct);
        if (incident.Status == IncidentStatus.Closed)
            throw new ConflictException($"Incident {incidentId} is closed.");

        var unit =
            await db.Units.FindAsync([unitId], ct)
            ?? throw new NotFoundException($"Unit {unitId} not found.");
        if (unit.Status != UnitStatus.Available)
            throw new ConflictException(
                $"Unit {unit.CallSign} is not available (status {unit.Status})."
            );

        incident.Assignments.Add(new Assignment { Unit = unit, AssignedAt = clock.GetUtcNow() });
        unit.Status = UnitStatus.Dispatched;
        if (incident.Status == IncidentStatus.New)
            incident.Status = IncidentStatus.Dispatched;

        await db.SaveChangesAsync(ct);
        return await NotifyUpdatedAsync(incident, ct);
    }

    public async Task<IncidentResponse> ChangeStatusAsync(
        int incidentId,
        IncidentStatus newStatus,
        CancellationToken ct
    )
    {
        var incident = await LoadAsync(incidentId, ct);
        if (!AllowedTransitions[incident.Status].Contains(newStatus))
            throw new ConflictException(
                $"Cannot change incident status from {incident.Status} to {newStatus}."
            );

        var activeAssignments = incident.Assignments.Where(a => a.ReleasedAt is null).ToList();
        var now = clock.GetUtcNow();

        switch (newStatus)
        {
            case IncidentStatus.OnScene:
                if (activeAssignments.Count == 0)
                    throw new ConflictException(
                        $"Incident {incidentId} has no assigned unit that could be on scene."
                    );
                foreach (var assignment in activeAssignments)
                    assignment.Unit.Status = UnitStatus.OnScene;
                break;
            case IncidentStatus.Closed:
                foreach (var assignment in activeAssignments)
                {
                    assignment.ReleasedAt = now;
                    assignment.Unit.Status = UnitStatus.Available;
                }
                incident.ClosedAt = now;
                break;
        }

        incident.Status = newStatus;
        await db.SaveChangesAsync(ct);
        return await NotifyUpdatedAsync(incident, ct);
    }

    private IQueryable<Incident> IncidentsWithAssignments() =>
        db.Incidents.Include(i => i.Assignments).ThenInclude(a => a.Unit);

    private async Task<Incident> LoadAsync(int id, CancellationToken ct) =>
        await IncidentsWithAssignments().FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException($"Incident {id} not found.");

    // Assigning and status changes also change unit statuses, so both caches go.
    private async Task<IncidentResponse> NotifyUpdatedAsync(Incident incident, CancellationToken ct)
    {
        await cache.InvalidateIncidentsAsync(ct);
        await cache.InvalidateUnitsAsync(ct);
        var response = incident.ToResponse();
        await notifier.IncidentUpdatedAsync(response);
        return response;
    }
}
