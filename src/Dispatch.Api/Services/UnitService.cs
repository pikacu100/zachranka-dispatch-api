using Dispatch.Api.Contracts;
using Dispatch.Api.Data;
using Dispatch.Api.Domain;
using Dispatch.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Services;

public class UnitService(DispatchDbContext db)
{
    public async Task<IReadOnlyList<UnitResponse>> GetAllAsync(CancellationToken ct) =>
        await db
            .Units.OrderBy(u => u.CallSign)
            .Select(u => new UnitResponse(u.Id, u.CallSign, u.Type, u.Status))
            .ToListAsync(ct);

    public async Task<UnitResponse> GetAsync(int id, CancellationToken ct) =>
        (await db.Units.FindAsync([id], ct))?.ToResponse()
        ?? throw new NotFoundException($"Unit {id} not found.");

    public async Task<UnitResponse> CreateAsync(CreateUnitRequest request, CancellationToken ct)
    {
        var callSign = request.CallSign.Trim();
        if (await db.Units.AnyAsync(u => u.CallSign == callSign, ct))
            throw new ConflictException($"Unit with call sign {callSign} already exists.");

        var unit = new Unit { CallSign = callSign, Type = request.Type };
        db.Units.Add(unit);
        await db.SaveChangesAsync(ct);
        return unit.ToResponse();
    }
}
