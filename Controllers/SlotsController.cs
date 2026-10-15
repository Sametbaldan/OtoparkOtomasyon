using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;
using Otopark.Services;

namespace Otopark.Controllers;

public record SlotCreate(string Code);

[ApiController, Route("api/slots"), Authorize]
public class SlotsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var active = await db.ParkingRecords.Where(r => r.ExitTime == null).ToListAsync();
        var slots = await db.Slots.OrderBy(s => s.Id).ToListAsync();
        var items = slots.Select(s =>
        {
            var r = active.FirstOrDefault(a => a.SlotId == s.Id);
            return new { s.Id, s.Code, s.IsActive, Occupied = r != null, Plate = r?.Plate, Since = r?.EntryTime, VehicleType = r?.VehicleType };
        }).ToList();
        var total = items.Count(i => i.IsActive);
        var occ = items.Count(i => i.Occupied);
        return Ok(new { total, occupied = occ, free = total - occ, rate = total == 0 ? 0 : Math.Round(occ * 100.0 / total, 1), slots = items });
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(SlotCreate req)
    {
        var code = Plates.Normalize(req.Code);
        if (code.Length is < 1 or > 10) return BadRequest(new { message = "Yer kodu 1-10 karakter olmalı." });
        if (await db.Slots.AnyAsync(s => s.Code == code)) return Conflict(new { message = "Bu yer kodu zaten var." });
        var slot = new ParkingSlot { Code = code };
        db.Slots.Add(slot);
        await db.SaveChangesAsync();
        return Ok(slot);
    }

    [HttpPut("{id:int}/toggle"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Toggle(int id)
    {
        var slot = await db.Slots.FindAsync(id);
        if (slot == null) return NotFound();
        if (slot.IsActive && await db.ParkingRecords.AnyAsync(r => r.ExitTime == null && r.SlotId == id))
            return Conflict(new { message = "Dolu bir yer kapatılamaz." });
        slot.IsActive = !slot.IsActive;
        await db.SaveChangesAsync();
        return Ok(slot);
    }
}
