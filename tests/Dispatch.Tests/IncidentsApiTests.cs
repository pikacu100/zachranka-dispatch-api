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

    // The database is shared by all tests in this class, so the filter tests check
    // "every result matches and ours is (not) there" instead of exact counts.

    [Fact]
    public async Task Filter_by_status_returns_only_matching_incidents()
    {
        var open = await CreateIncidentAsync();
        var closed = await CreateIncidentAsync();
        await ChangeStatusAsync(closed.Id, IncidentStatus.Closed);

        var result = await GetIncidentsAsync("?status=Closed");

        Assert.All(result, i => Assert.Equal(IncidentStatus.Closed, i.Status));
        Assert.Contains(result, i => i.Id == closed.Id);
        Assert.DoesNotContain(result, i => i.Id == open.Id);
    }

    [Fact]
    public async Task Filter_by_status_and_priority_combines_both_conditions()
    {
        var critical = await CreateIncidentAsync(IncidentPriority.Critical);
        var low = await CreateIncidentAsync(IncidentPriority.Low);

        var result = await GetIncidentsAsync("?status=New&priority=Critical");

        Assert.All(result, i =>
        {
            Assert.Equal(IncidentStatus.New, i.Status);
            Assert.Equal(IncidentPriority.Critical, i.Priority);
        });
        Assert.Contains(result, i => i.Id == critical.Id);
        Assert.DoesNotContain(result, i => i.Id == low.Id);
    }

    [Fact]
    public async Task Filter_with_unknown_status_returns_bad_request()
    {
        var response = await _client.GetAsync("api/incidents?status=Nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<UnitResponse> CreateUnitAsync()
    {
        var callSign = $"RZP {Random.Shared.Next(100, 99999)}";
        var response = await _client.PostAsJsonAsync("api/units",
            new CreateUnitRequest(callSign, UnitType.Ambulance), DispatchApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<UnitResponse>(response);
    }

    private async Task<IncidentResponse> CreateIncidentAsync(IncidentPriority priority = IncidentPriority.High)
    {
        var response = await _client.PostAsJsonAsync("api/incidents",
            new CreateIncidentRequest("Dopravní nehoda", "Dvě osobní auta", "Brno, Hlinky 12", priority),
            DispatchApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<IncidentResponse>(response);
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(int incidentId, IncidentStatus status) =>
        _client.PutAsJsonAsync($"api/incidents/{incidentId}/status",
            new ChangeIncidentStatusRequest(status), DispatchApiFactory.Json);

    private async Task<List<IncidentResponse>> GetIncidentsAsync(string query)
    {
        var response = await _client.GetAsync($"api/incidents{query}");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<List<IncidentResponse>>(response);
    }

    private async Task<UnitResponse> GetUnitAsync(int id) =>
        await ReadAsync<UnitResponse>(await _client.GetAsync($"api/units/{id}"));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(DispatchApiFactory.Json))!;
}
