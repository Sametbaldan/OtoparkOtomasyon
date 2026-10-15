using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;

namespace Otopark.Controllers;

[ApiController, Route("api/reports"), Authorize(Roles = Roles.Admin)]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var today = DateTime.Today;
        var week = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Pazartesi
        var month = new DateTime(today.Year, today.Month, 1);
        var since = month < week ? month : week;
        var pays = await db.Payments.Where(p => p.Date >= since).ToListAsync();

        object Sum(DateTime from)
        {
            var l = pays.Where(p => p.Date >= from).ToList();
            return new
            {
                total = l.Sum(p => p.Amount),
                nakit = l.Where(p => p.Type == PaymentType.Nakit).Sum(p => p.Amount),
                krediKarti = l.Where(p => p.Type == PaymentType.KrediKarti).Sum(p => p.Amount),
                park = l.Where(p => p.Kind == PaymentKind.Park).Sum(p => p.Amount),
                abonelik = l.Where(p => p.Kind == PaymentKind.Abonelik).Sum(p => p.Amount)
            };
        }

        return Ok(new
        {
            today = Sum(today), week = Sum(week), month = Sum(month),
            inside = await db.ParkingRecords.CountAsync(r => r.ExitTime == null)
        });
    }

    [HttpGet("daily")]
    public async Task<IActionResult> Daily([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var from = DateTime.Today.AddDays(-(days - 1));
        var pays = await db.Payments.Where(p => p.Date >= from).ToListAsync();
        return Ok(Enumerable.Range(0, days).Select(i =>
        {
            var d = from.AddDays(i);
            return new { date = d.ToString("dd.MM"), amount = pays.Where(p => p.Date.Date == d).Sum(p => p.Amount) };
        }));
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics()
    {
        var from = DateTime.Today.AddDays(-90);
        var recs = await db.ParkingRecords.Where(r => r.EntryTime >= from).ToListAsync();
        var hourly = Enumerable.Range(0, 24).Select(h => recs.Count(r => r.EntryTime.Hour == h)).ToArray();
        string[] gun = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        var weekday = gun.Select((_, i) => recs.Count(r => ((int)r.EntryTime.DayOfWeek + 6) % 7 == i)).ToArray();
        var done = recs.Where(r => r.ExitTime != null).ToList();
        var avg = done.Count == 0 ? 0 : Math.Round(done.Average(r => (r.ExitTime!.Value - r.EntryTime).TotalMinutes), 1);
        var byType = recs.GroupBy(r => r.VehicleType).Select(g => new { type = g.Key.ToString(), count = g.Count() });
        return Ok(new { hourly, weekday, gun, avgMinutes = avg, byType, peakHour = Array.IndexOf(hourly, hourly.Max()) });
    }
}
