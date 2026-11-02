using System.ComponentModel.DataAnnotations;
using Dispatch.Api.Domain;

namespace Dispatch.Api.Contracts;

public record CreateUnitRequest(
    [Required, StringLength(32, MinimumLength = 2)] string CallSign,
    [EnumDataType(typeof(UnitType))] UnitType Type
);

public record UnitResponse(int Id, string CallSign, UnitType Type, UnitStatus Status);

public record CreateIncidentRequest(
    [Required, StringLength(200, MinimumLength = 3)] string Title,
    [StringLength(2000)] string? Description,
    [Required, StringLength(300, MinimumLength = 3)] string Address,
    [EnumDataType(typeof(IncidentPriority))] IncidentPriority Priority
);

public record AssignUnitRequest([Range(1, int.MaxValue)] int UnitId);

public record ChangeIncidentStatusRequest(
    [EnumDataType(typeof(IncidentStatus))] IncidentStatus Status
);

public record AssignmentResponse(
    int UnitId,
    string CallSign,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ReleasedAt
);

public record IncidentResponse(
    int Id,
    string Title,
    string? Description,
    string Address,
    IncidentPriority Priority,
    IncidentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<AssignmentResponse> Assignments
);

public static class Mapping
{
    public static UnitResponse ToResponse(this Unit unit) =>
        new(unit.Id, unit.CallSign, unit.Type, unit.Status);

    public static IncidentResponse ToResponse(this Incident incident) =>
        new(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Address,
            incident.Priority,
            incident.Status,
            incident.CreatedAt,
            incident.ClosedAt,
            incident
                .Assignments.OrderBy(a => a.Id)
                .Select(a => new AssignmentResponse(
                    a.UnitId,
                    a.Unit.CallSign,
                    a.AssignedAt,
                    a.ReleasedAt
                ))
                .ToList()
        );
}
