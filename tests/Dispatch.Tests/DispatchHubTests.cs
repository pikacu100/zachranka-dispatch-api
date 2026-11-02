using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection; // AddJsonProtocol

namespace Dispatch.Tests;

public class DispatchHubTests(DispatchApiFactory factory) : IClassFixture<DispatchApiFactory>
{
    [Fact]
    public async Task Connected_client_is_notified_about_new_incident()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "hubs/dispatch"),
                o =>
                {
                    o.Transports = HttpTransportType.LongPolling;
                    o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                }
            )
            .AddJsonProtocol(o =>
                o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())
            )
            .Build();

        var received = new TaskCompletionSource<IncidentResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        connection.On<IncidentResponse>(
            "IncidentCreated",
            incident => received.TrySetResult(incident)
        );
        await connection.StartAsync();

        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "api/incidents",
            new CreateIncidentRequest(
                "Požár garáže",
                null,
                "Žďár nad Sázavou, Nádražní 5",
                IncidentPriority.Critical
            ),
            DispatchApiFactory.Json
        );

        var incident = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Požár garáže", incident.Title);
        Assert.Equal(IncidentPriority.Critical, incident.Priority);
    }
}
