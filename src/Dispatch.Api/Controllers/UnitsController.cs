using Dispatch.Api.Contracts;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

[ApiController]
[Route("api/units")]
public class UnitsController(UnitService units) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UnitResponse>> GetAll(CancellationToken ct) =>
        units.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<UnitResponse> Get(int id, CancellationToken ct) =>
        units.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnitResponse>> Create(CreateUnitRequest request, CancellationToken ct)
    {
        var unit = await units.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = unit.Id }, unit);
    }
}
