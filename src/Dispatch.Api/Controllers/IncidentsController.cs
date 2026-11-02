using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

/// <summary>
/// Incidents (emergency calls): creating them, assigning response units and moving them through their states.
/// </summary>
[ApiController]
[Route("api/incidents")]
public class IncidentsController(IncidentService incidents) : ControllerBase
{
    /// <summary>
    /// Returns all incidents, newest first, including their assigned units.
    /// </summary>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <param name="status">Status of the incidents</param>
    /// <param name="priority">Priority of incidents</param>
    [HttpGet]
    public Task<IReadOnlyList<IncidentResponse>> GetAll([FromQuery] IncidentStatus? status, [FromQuery] IncidentPriority? priority, CancellationToken ct) =>
        incidents.GetAllAsync(ct: ct, status: status, priority: priority);

    /// <summary>
    /// Returns one incident with its assigned units.
    /// </summary>
    /// <param name="id">Incident ID.</param>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <response code="404">The incident doesn't exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IncidentResponse> Get(int id, CancellationToken ct) =>
        incidents.GetAsync(id, ct);

    /// <summary>
    /// Creates a new incident in the <c>New</c> state and notifies connected dispatchers over SignalR.
    /// </summary>
    /// <param name="request">Title, address, priority and an optional description.</param>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <response code="201">The incident was created; the Location header points to it.</response>
    /// <response code="400">The request failed validation (e.g. missing title or address).</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IncidentResponse>> Create(CreateIncidentRequest request, CancellationToken ct)
    {
        var incident = await incidents.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = incident.Id }, incident);
    }

    /// <summary>
    /// Sends an available unit to the incident. The unit becomes <c>Dispatched</c>,
    /// and a <c>New</c> incident becomes <c>Dispatched</c> too.
    /// </summary>
    /// <param name="id">Incident ID.</param>
    /// <param name="request">The unit to send.</param>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <response code="404">The incident or the unit doesn't exist.</response>
    /// <response code="409">The incident is closed or the unit isn't available.</response>
    [HttpPost("{id:int}/assignments")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IncidentResponse> AssignUnit(int id, AssignUnitRequest request, CancellationToken ct) =>
        incidents.AssignUnitAsync(id, request.UnitId, ct);

    /// <summary>
    /// Changes the incident state. <c>OnScene</c> moves its units on scene;
    /// <c>Closed</c> releases them back to <c>Available</c>.
    /// </summary>
    /// <param name="id">Incident ID.</param>
    /// <param name="request">The new incident status.</param>
    /// <param name="ct">Cancelled when the client aborts the request.</param>
    /// <response code="404">The incident doesn't exist.</response>
    /// <response code="409">The transition isn't allowed (e.g. <c>New</c> straight to <c>OnScene</c>).</response>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IncidentResponse> ChangeStatus(int id, ChangeIncidentStatusRequest request, CancellationToken ct) =>
        incidents.ChangeStatusAsync(id, request.Status, ct);

}
