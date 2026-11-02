using Dispatch.Api.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Dispatch.Api.Hubs;

/// <summary>Methods the server calls on connected clients (dispatcher screens).</summary>
public interface IDispatchClient
{
    Task IncidentCreated(IncidentResponse incident);
    Task IncidentUpdated(IncidentResponse incident);
}

/// <summary>Clients only listen; all changes go through the REST API.</summary>
public class DispatchHub : Hub<IDispatchClient>;

public interface IDispatchNotifier
{
    Task IncidentCreatedAsync(IncidentResponse incident);
    Task IncidentUpdatedAsync(IncidentResponse incident);
}

public class SignalRDispatchNotifier(IHubContext<DispatchHub, IDispatchClient> hub)
    : IDispatchNotifier
{
    public Task IncidentCreatedAsync(IncidentResponse incident) =>
        hub.Clients.All.IncidentCreated(incident);

    public Task IncidentUpdatedAsync(IncidentResponse incident) =>
        hub.Clients.All.IncidentUpdated(incident);
}
