namespace Dispatch.Api.Domain;

/// <summary>A unit sent to an incident. ReleasedAt is set when the incident is closed.</summary>
public class Assignment
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}
