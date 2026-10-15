using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;
using Otopark.Services;

namespace Otopark.Controllers;

public record EntryRequest(string Plate, VehicleType VehicleType);
public record ExitRequest(string Query, PaymentType? PaymentType);

[ApiController, Route("api/parking"), Authorize]
public class ParkingController(AppDbContext db, PricingService pricing) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("entry")]
    public async Task<IActionResult> Entry(EntryRequest req)
    {
        var plate = Plates.Normalize(req.Plate);
        if (!Plates.IsValid(plate)) return BadRequest(new { message = "Geçersiz plaka biçimi (örn. 34ABC123)." });
        if (await db.ParkingRecords.AnyAsync(r => r.Plate == plate && r.ExitTime == null))
            return Conflict(new { message = "Bu araç zaten otoparkta görünüyor." });

        var occupied = db.ParkingRecords.Where(r => r.ExitTime == null && r.SlotId != null).Select(r => r.SlotId!.Value);
        var slot = await db.Slots.Where(s => s.IsActive && !occupied.Contains(s.Id)).OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (slot == null) return Conflict(new { message = "Otopark dolu, boş yer yok." });

        var now = DateTime.Now;
        var rec = new ParkingRecord
        {
            Plate = plate, VehicleType = req.VehicleType, EntryTime = now, SlotId = slot.Id,
            TicketCode = Guid.NewGuid().ToString("N")[..10].ToUpper(), CreatedByUserId = UserId
        };
        db.ParkingRecords.Add(rec);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // Aynı anda başka bir giriş aynı yeri/plakayı almış olabilir
            return Conflict(new { message = "Kayıt çakıştı (aynı yer ya da plaka). Lütfen tekrar deneyin." });
        }

        var (active, warning) = await pricing.CheckSubscriptionAsync(plate, now);
        return Ok(new
        {
            rec.Id, rec.Plate, rec.VehicleType, rec.TicketCode, Slot = slot.Code, rec.EntryTime,
            IsSubscriber = active, Warning = warning
        });
    }

    [HttpGet("quote")]
    public async Task<IActionResult> Quote([FromQuery] string query)
    {
        var rec = await FindActive(query);
        if (rec == null) return NotFound(new { message = "Aktif kayıt bulunamadı." });
        var now = DateTime.Now;
        return Ok(ToQuote(rec, await pricing.CalculateAsync(rec, now), now));
    }

    [HttpPost("exit")]
    public async Task<IActionResult> Exit(ExitRequest req)
    {
        var rec = await FindActive(req.Query);
        if (rec == null) return NotFound(new { message = "Aktif kayıt bulunamadı." });

        var now = DateTime.Now;
        var q = await pricing.CalculateAsync(rec, now);
        if (q.Fee > 0 && req.PaymentType == null)
            return BadRequest(new { message = "Ödeme tipini seçin." });

        rec.ExitTime = now;
        rec.Fee = q.Fee;
        rec.IsSubscriber = q.IsSubscriber;
        if (q.Fee > 0)
            db.Payments.Add(new Payment
            {
                Date = now, Amount = q.Fee, Type = req.PaymentType!.Value,
                Kind = PaymentKind.Park, RecordId = rec.Id, UserId = UserId
            });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Bu araç için çıkış zaten işlenmiş." });
        }
        return Ok(ToQuote(rec, q, now));
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active()
    {
        var now = DateTime.Now;
        var list = await db.ParkingRecords.Include(r => r.Slot)
            .Where(r => r.ExitTime == null).OrderByDescending(r => r.EntryTime).ToListAsync();
        return Ok(list.Select(r => new
        {
            r.Id, r.Plate, r.VehicleType, r.TicketCode, Slot = r.Slot?.Code, r.EntryTime,
            Minutes = (int)(now - r.EntryTime).TotalMinutes
        }));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int take = 100, [FromQuery] string? plate = null)
    {
        take = Math.Clamp(take, 1, 500);
        var q = db.ParkingRecords.Where(r => r.ExitTime != null);
        var p = Plates.Normalize(plate);
        if (p.Length > 0) q = q.Where(r => r.Plate.Contains(p));
        var list = await q.OrderByDescending(r => r.ExitTime).Take(take).ToListAsync();
        return Ok(list.Select(r => new
        {
            r.Id, r.Plate, r.VehicleType, r.EntryTime, r.ExitTime, r.Fee, r.IsSubscriber,
            Minutes = (int)(r.ExitTime!.Value - r.EntryTime).TotalMinutes
        }));
    }

    /// Dijital fiş: QR/SMS ile gönderilen bağlantı için herkese açık, yalnızca okuma. Plaka maskelenir.
    [HttpGet("ticket/{code}"), AllowAnonymous]
    public async Task<IActionResult> Ticket(string code)
    {
        var rec = await db.ParkingRecords.Include(r => r.Slot)
            .FirstOrDefaultAsync(r => r.TicketCode == code.ToUpperInvariant());
        if (rec == null) return NotFound(new { message = "Fiş bulunamadı." });

        var at = rec.ExitTime ?? DateTime.Now;
        var q = rec.ExitTime == null
            ? await pricing.CalculateAsync(rec, at)
            : new FeeResult(rec.Fee, (int)(rec.ExitTime.Value - rec.EntryTime).TotalMinutes, rec.IsSubscriber, null);
        return Ok(new
        {
            rec.TicketCode, Plate = Mask(rec.Plate), rec.VehicleType, Slot = rec.Slot?.Code, rec.EntryTime, rec.ExitTime,
            Status = rec.ExitTime == null ? "Otoparkta" : "Çıkış yapıldı",
            q.Minutes, q.Fee, q.IsSubscriber
        });
    }

    private static string Mask(string plate) =>
        plate.Length <= 4 ? plate : plate[..2] + new string('*', plate.Length - 4) + plate[^2..];

    private async Task<ParkingRecord?> FindActive(string? query)
    {
        var q = (query ?? "").Trim().ToUpperInvariant();
        var plate = Plates.Normalize(q);
        return await db.ParkingRecords.Include(r => r.Slot)
            .FirstOrDefaultAsync(r => r.ExitTime == null && (r.TicketCode == q || r.Plate == plate));
    }

    private static object ToQuote(ParkingRecord r, FeeResult q, DateTime now) => new
    {
        r.Id, r.Plate, r.VehicleType, r.TicketCode, Slot = r.Slot?.Code, r.EntryTime, ExitTime = now,
        q.Minutes, q.Fee, q.IsSubscriber, q.Warning
    };
}
