namespace Dispatch.Api.Domain;

public enum UnitType
{
    Ambulance,
    Fire,
    Police,
}

public enum UnitStatus
{
    Available,
    Dispatched,
    OnScene,
    OutOfService,
}

public enum IncidentPriority
{
    Low,
    Medium,
    High,
    Critical,
}

public enum IncidentStatus
{
    New,
    Dispatched,
    OnScene,
    Closed,
}
