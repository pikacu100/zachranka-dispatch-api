namespace Dispatch.Api.Domain;

/// <summary>A response unit, e.g. an ambulance crew with the call sign "RZP 101".</summary>
public class Unit
{
    public int Id { get; set; }
    public required string CallSign { get; set; }
    public UnitType Type { get; set; }
    public UnitStatus Status { get; set; } = UnitStatus.Available;

    public List<Assignment> Assignments { get; set; } = [];
}
