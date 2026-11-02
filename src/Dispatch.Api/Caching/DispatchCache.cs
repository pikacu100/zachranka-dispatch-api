using Microsoft.AspNetCore.OutputCaching;

namespace Dispatch.Api.Caching;

/// <summary>
/// Output cache for the read endpoints. GET responses are cached per URL (including the
/// filter query) and tagged; every write evicts the affected tags, so a reader never sees
/// stale data while repeated reads (e.g. several dispatcher screens refreshing at once)
/// don't hit the database.
/// </summary>
public static class CachePolicies
{
    public const string Incidents = "incidents";
    public const string Units = "units";

    // Safety net only: writes evict the cache immediately, this just bounds memory use.
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddDispatchOutputCache(this IServiceCollection services) =>
        services.AddOutputCache(o =>
        {
            o.AddPolicy(
                Incidents,
                b => b.Tag(Incidents).SetVaryByQuery("status", "priority").Expire(Expiration)
            );
            o.AddPolicy(Units, b => b.Tag(Units).Expire(Expiration));
        });
}

public class DispatchCache(IOutputCacheStore store)
{
    public async Task InvalidateIncidentsAsync(CancellationToken ct) =>
        await store.EvictByTagAsync(CachePolicies.Incidents, ct);

    public async Task InvalidateUnitsAsync(CancellationToken ct) =>
        await store.EvictByTagAsync(CachePolicies.Units, ct);
}
