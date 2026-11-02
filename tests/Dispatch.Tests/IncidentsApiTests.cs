using System.Net;
using System.Net.Http.Json;
using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;

namespace Dispatch.Tests;

public class IncidentsApiTests(DispatchApiFactory factory) : IClassFixture<DispatchApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Assigning_unit_dispatches_incident_and_unit()
    {
        var unit = await CreateUnitAsync();
        var incident = await CreateIncidentAsync();

        var response = await _client.PostAsJsonAsync($"api/incidents/{incident.Id}/assignments", new AssignUnitRequest(unit.Id));

        response.EnsureSuccessStatusCode();
        var updated = await ReadAsync<IncidentResponse>(response);
        Assert.Equal(IncidentStatus.Dispatched, updated.Status);
        Assert.Equal(unit.CallSign, Assert.Single(updated.Assignments).CallSign);
        Assert.Equal(UnitStatus.Dispatched, (await GetUnitAsync(unit.Id)).Status);
    }

    [Fact]
    public async Task Busy_unit_cannot_be_assigned_twice()
    {
        var unit = await CreateUnitAsync();
        var first = await CreateIncidentAsync();
        var second = await CreateIncidentAsync();
        await _client.PostAsJsonAsync($"api/incidents/{first.Id}/assignments", new AssignUnitRequest(unit.Id));

        var response = await _client.PostAsJsonAsync($"api/incidents/{second.Id}/assignments", new AssignUnitRequest(unit.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task On_scene_then_closed_releases_units()
    {
        var unit = await CreateUnitAsync();
        var incident = await CreateIncidentAsync();
        await _client.PostAsJsonAsync($"api/incidents/{incident.Id}/assignments", new AssignUnitRequest(unit.Id));

        var onScene = await ChangeStatusAsync(incident.Id, IncidentStatus.OnScene);
        onScene.EnsureSuccessStatusCode();
        Assert.Equal(UnitStatus.OnScene, (await GetUnitAsync(unit.Id)).Status);

        var closed = await ReadAsync<IncidentResponse>(await ChangeStatusAsync(incident.Id, IncidentStatus.Closed));
        Assert.Equal(IncidentStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
        Assert.NotNull(Assert.Single(closed.Assignments).ReleasedAt);
        Assert.Equal(UnitStatus.Available, (await GetUnitAsync(unit.Id)).Status);
    }

    [Fact]
    public async Task New_incident_cannot_skip_to_on_scene()
    {
        var incident = await CreateIncidentAsync();

        var response = await ChangeStatusAsync(incident.Id, IncidentStatus.OnScene);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Closed_incident_cannot_get_new_units()
    {
        var unit = await CreateUnitAsync();
        var incident = await CreateIncidentAsync();
        await ChangeStatusAsync(incident.Id, IncidentStatus.Closed);

        var response = await _client.PostAsJsonAsync($"api/incidents/{incident.Id}/assignments", new AssignUnitRequest(unit.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_incident_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("api/incidents",
            new CreateIncidentRequest("", null, "Brno", IncidentPriority.High), DispatchApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_incident_returns_not_found()
    {
        var response = await _client.GetAsync("api/incidents/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_call_sign_is_rejected()
    {
        var unit = await CreateUnitAsync();

        var response = await _client.PostAsJsonAsync("api/units",
            new CreateUnitRequest(unit.CallSign, UnitType.Fire), DispatchApiFactory.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<UnitResponse> CreateUnitAsync()
    {
        var callSign = $"RZP {Random.Shared.Next(100, 99999)}";
        var response = await _client.PostAsJsonAsync("api/units",
            new CreateUnitRequest(callSign, UnitType.Ambulance), DispatchApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<UnitResponse>(response);
    }

    private async Task<IncidentResponse> CreateIncidentAsync()
    {
        var response = await _client.PostAsJsonAsync("api/incidents",
            new CreateIncidentRequest("Dopravní nehoda", "Dvě osobní auta", "Brno, Hlinky 12", IncidentPriority.High),
            DispatchApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<IncidentResponse>(response);
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(int incidentId, IncidentStatus status) =>
        _client.PutAsJsonAsync($"api/incidents/{incidentId}/status",
            new ChangeIncidentStatusRequest(status), DispatchApiFactory.Json);

    private async Task<UnitResponse> GetUnitAsync(int id) =>
        await ReadAsync<UnitResponse>(await _client.GetAsync($"api/units/{id}"));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(DispatchApiFactory.Json))!;
}
