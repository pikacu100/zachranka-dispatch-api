using Dispatch.Api.Caching;
using Dispatch.Api.Contracts;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Dispatch.Api.Controllers;

/// <summary>
/// Response units (ambulance crews, fire engines, police patrols) that can be sent to incidents.
/// </summary>
[ApiController]
[Route("api/units")]
public class UnitsController(UnitService units) : ControllerBase
{
    /// <summary>
    /// Returns all units ordered by call sign, with their current status.
    /// </summary>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    [OutputCache(PolicyName = CachePolicies.Units)]
    [HttpGet]
    public Task<IReadOnlyList<UnitResponse>> GetAll(CancellationToken ct) => units.GetAllAsync(ct);

    /// <summary>
    /// Returns one unit.
    /// </summary>
    /// <param name="id">Unit ID.</param>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <response code="404">The unit doesn't exist.</response>
    [OutputCache(PolicyName = CachePolicies.Units)]
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<UnitResponse> Get(int id, CancellationToken ct) => units.GetAsync(id, ct);

    /// <summary>
    /// Registers a new unit. It starts as <c>Available</c>.
    /// </summary>
    /// <param name="request">Call sign and unit type.</param>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <response code="201">The unit was created; the Location header points to it.</response>
    /// <response code="400">The request failed validation (e.g. call sign too short).</response>
    /// <response code="409">A unit with the same call sign already exists.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnitResponse>> Create(
        CreateUnitRequest request,
        CancellationToken ct
    )
    {
        var unit = await units.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = unit.Id }, unit);
    }
}
