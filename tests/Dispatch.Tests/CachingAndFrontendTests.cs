using System.Net;
using System.Net.Http.Json;
using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;

namespace Dispatch.Tests;

public class CachingAndFrontendTests(DispatchApiFactory factory) : IClassFixture<DispatchApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Repeated_read_is_served_from_output_cache()
    {
        await _client.GetAsync("api/units");

        var second = await _client.GetAsync("api/units");

        // The output cache adds an Age header only to responses it serves from the cache.
        Assert.True(second.Headers.Age.HasValue, "Second GET should come from the cache.");
    }

    [Fact]
    public async Task Creating_incident_evicts_cached_list()
    {
        await GetAsync<List<IncidentResponse>>("api/incidents"); // fills the cache

        var created = await PostAsync<IncidentResponse>(
            "api/incidents",
            new CreateIncidentRequest(
                "Pád ze žebříku",
                null,
                "Jihlava, Žižkova 40",
                IncidentPriority.Medium
            )
        );

        var incidents = await GetAsync<List<IncidentResponse>>("api/incidents");
        Assert.Contains(incidents, i => i.Id == created.Id);
    }

    [Fact]
    public async Task Assigning_unit_evicts_cached_units()
    {
        var unit = await PostAsync<UnitResponse>(
            "api/units",
            new CreateUnitRequest($"RZP {Random.Shared.Next(100, 99999)}", UnitType.Ambulance)
        );
        var incident = await PostAsync<IncidentResponse>(
            "api/incidents",
            new CreateIncidentRequest("Bolest břicha", null, "Brno, Bašty 2", IncidentPriority.High)
        );
        await GetAsync<List<UnitResponse>>("api/units"); // fills the cache with the unit as Available

        await PostAsync<IncidentResponse>(
            $"api/incidents/{incident.Id}/assignments",
            new AssignUnitRequest(unit.Id)
        );

        var units = await GetAsync<List<UnitResponse>>("api/units");
        Assert.Equal(UnitStatus.Dispatched, units.Single(u => u.Id == unit.Id).Status);
    }

    [Fact]
    public async Task Filtered_lists_are_cached_separately()
    {
        await PostAsync<IncidentResponse>(
            "api/incidents",
            new CreateIncidentRequest(
                "Kolaps v hale",
                null,
                "Olomouc, Horní nám. 7",
                IncidentPriority.Low
            )
        );

        var low = await GetAsync<List<IncidentResponse>>("api/incidents?priority=Low");
        var critical = await GetAsync<List<IncidentResponse>>("api/incidents?priority=Critical");

        Assert.All(low, i => Assert.Equal(IncidentPriority.Low, i.Priority));
        Assert.All(critical, i => Assert.Equal(IncidentPriority.Critical, i.Priority));
        Assert.NotEmpty(low);
    }

    [Fact]
    public async Task Dispatcher_board_is_served_with_revalidation_headers()
    {
        var page = await _client.GetAsync("/");
        var script = await _client.GetAsync("js/app.js");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Záchranka Dispatch", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.True(script.Headers.CacheControl?.NoCache);
        Assert.NotNull(script.Headers.ETag);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(DispatchApiFactory.Json))!;
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body, DispatchApiFactory.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(DispatchApiFactory.Json))!;
    }
}
