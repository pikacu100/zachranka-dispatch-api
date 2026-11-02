using Dispatch.Api.Contracts;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public class IncidentsController(IncidentService incidents) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<IncidentResponse>> GetAll(CancellationToken ct) =>
        incidents.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IncidentResponse> Get(int id, CancellationToken ct) =>
        incidents.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IncidentResponse>> Create(CreateIncidentRequest request, CancellationToken ct)
    {
        var incident = await incidents.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = incident.Id }, incident);
    }

    [HttpPost("{id:int}/assignments")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IncidentResponse> AssignUnit(int id, AssignUnitRequest request, CancellationToken ct) =>
        incidents.AssignUnitAsync(id, request.UnitId, ct);

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IncidentResponse> ChangeStatus(int id, ChangeIncidentStatusRequest request, CancellationToken ct) =>
        incidents.ChangeStatusAsync(id, request.Status, ct);
}
