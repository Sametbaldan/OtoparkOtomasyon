using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;

namespace Otopark.Controllers;

public record TariffUpdate(int FreeMinutes, decimal FirstHourFee, decimal NextHourFee, decimal DailyMaxFee,
    decimal NightFee, int NightStartHour, int NightEndHour);

[ApiController, Route("api/tariffs"), Authorize]
public class TariffsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.Tariffs.OrderBy(t => t.Id).ToListAsync());

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, TariffUpdate u)
    {
        var t = await db.Tariffs.FindAsync(id);
        if (t == null) return NotFound();
        if (u.FirstHourFee < 0 || u.NextHourFee < 0 || u.DailyMaxFee < 0 || u.NightFee < 0 || u.FreeMinutes < 0
            || u.NightStartHour is < 0 or > 23 || u.NightEndHour is < 0 or > 23)
            return BadRequest(new { message = "Geçersiz değer." });

        t.FreeMinutes = u.FreeMinutes; t.FirstHourFee = u.FirstHourFee; t.NextHourFee = u.NextHourFee;
        t.DailyMaxFee = u.DailyMaxFee; t.NightFee = u.NightFee;
        t.NightStartHour = u.NightStartHour; t.NightEndHour = u.NightEndHour;
        await db.SaveChangesAsync();
        return Ok(t);
    }
}
