using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;
using Otopark.Services;

namespace Otopark.Controllers;

public record SubscriptionRequest(string Plate, string OwnerName, string? Phone, int Months, decimal Fee, PaymentType PaymentType);
public record RenewRequest(int Months, decimal Fee, PaymentType PaymentType);

[ApiController, Route("api/subscriptions"), Authorize]
public class SubscriptionsController(AppDbContext db) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var today = DateTime.Today;
        var items = await db.Subscriptions.OrderByDescending(s => s.EndDate).ToListAsync();
        return Ok(items.Select(s =>
        {
            var left = (s.EndDate.Date - today).Days;
            var status = !s.IsActive ? "Pasif" : left < 0 ? "Süresi Doldu" : left <= 7 ? "Yakında Bitiyor" : "Aktif";
            return new { s.Id, s.Plate, s.OwnerName, s.Phone, s.StartDate, s.EndDate, s.IsActive, DaysLeft = left, Status = status };
        }));
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(SubscriptionRequest req)
    {
        var plate = Plates.Normalize(req.Plate);
        if (!Plates.IsValid(plate)) return BadRequest(new { message = "Geçersiz plaka biçimi." });
        if (req.Months < 1 || req.Months > 36) return BadRequest(new { message = "Süre 1-36 ay olmalı." });
        if (string.IsNullOrWhiteSpace(req.OwnerName)) return BadRequest(new { message = "Ad soyad gerekli." });

        var today = DateTime.Today;
        if (await db.Subscriptions.AnyAsync(s => s.Plate == plate && s.IsActive && s.EndDate >= today))
            return Conflict(new { message = "Bu plakanın aktif aboneliği var; yenileme yapın." });

        var sub = new Subscription
        {
            Plate = plate, OwnerName = req.OwnerName.Trim(), Phone = req.Phone,
            StartDate = today, EndDate = today.AddMonths(req.Months)
        };
        db.Subscriptions.Add(sub);
        if (req.Fee > 0)
            db.Payments.Add(new Payment { Date = DateTime.Now, Amount = req.Fee, Type = req.PaymentType, Kind = PaymentKind.Abonelik, UserId = UserId });
        await db.SaveChangesAsync();
        return Ok(sub);
    }

    [HttpPut("{id:int}/renew"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Renew(int id, RenewRequest req)
    {
        var sub = await db.Subscriptions.FindAsync(id);
        if (sub == null) return NotFound();
        if (req.Months < 1 || req.Months > 36) return BadRequest(new { message = "Süre 1-36 ay olmalı." });

        var baseDate = sub.EndDate.Date > DateTime.Today ? sub.EndDate.Date : DateTime.Today;
        sub.EndDate = baseDate.AddMonths(req.Months);
        sub.IsActive = true;
        if (req.Fee > 0)
            db.Payments.Add(new Payment { Date = DateTime.Now, Amount = req.Fee, Type = req.PaymentType, Kind = PaymentKind.Abonelik, UserId = UserId });
        await db.SaveChangesAsync();
        return Ok(sub);
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var sub = await db.Subscriptions.FindAsync(id);
        if (sub == null) return NotFound();
        sub.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
