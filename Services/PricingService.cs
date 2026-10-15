using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;

namespace Otopark.Services;

public record FeeResult(decimal Fee, int Minutes, bool IsSubscriber, string? Warning);

public class PricingService(AppDbContext db)
{
    /// Abonelik durumu: (aktif mi, uyarı metni)
    public async Task<(bool Active, string? Warning)> CheckSubscriptionAsync(string plate, DateTime at)
    {
        var sub = await db.Subscriptions.Where(s => s.Plate == plate && s.IsActive)
            .OrderByDescending(s => s.EndDate).FirstOrDefaultAsync();
        if (sub == null) return (false, null);

        var today = at.Date;
        if (sub.StartDate.Date <= today && sub.EndDate.Date >= today)
        {
            var left = (sub.EndDate.Date - today).Days;
            return (true, left <= 7 ? $"Abonelik {left} gün içinde sona eriyor." : null);
        }
        return (false, $"Abonelik {sub.EndDate:dd.MM.yyyy} tarihinde sona ermiş; normal tarife uygulanır.");
    }

    public async Task<FeeResult> CalculateAsync(ParkingRecord r, DateTime exit)
    {
        var minutes = (int)Math.Max(0, (exit - r.EntryTime).TotalMinutes);
        var (active, warning) = await CheckSubscriptionAsync(r.Plate, exit);
        if (active) return new FeeResult(0, minutes, true, warning);

        var t = await db.Tariffs.FirstAsync(x => x.VehicleType == r.VehicleType);
        return new FeeResult(Compute(t, r.EntryTime, exit), minutes, false, warning);
    }

    /// Tarife kuralları:
    ///  1) FreeMinutes içinde çıkış ücretsiz
    ///  2) İlk saat + sonraki saat başı ücret (başlanan saat tam sayılır)
    ///  3) Her 24 saatlik dilim için DailyMaxFee tavanı
    ///  4) Giriş ve çıkış gece diliminde ve süre ≤ 12 saat ise NightFee (daha ucuzsa) uygulanır
    public static decimal Compute(Tariff t, DateTime entry, DateTime exit)
    {
        var minutes = (exit - entry).TotalMinutes;
        if (minutes <= t.FreeMinutes) return 0;

        decimal Hourly(double mins)
        {
            var hours = (int)Math.Ceiling(mins / 60.0);
            var fee = t.FirstHourFee + Math.Max(0, hours - 1) * t.NextHourFee;
            return Math.Min(fee, t.DailyMaxFee);
        }

        var fullDays = (int)(minutes / 1440);
        var rem = minutes - fullDays * 1440;
        var total = fullDays * t.DailyMaxFee + (rem > 0 ? Hourly(rem) : 0);

        bool Night(DateTime d) => t.NightStartHour > t.NightEndHour
            ? d.Hour >= t.NightStartHour || d.Hour < t.NightEndHour
            : d.Hour >= t.NightStartHour && d.Hour < t.NightEndHour;

        if (t.NightFee > 0 && fullDays == 0 && minutes <= 720 && Night(entry) && Night(exit) && total > t.NightFee)
            total = t.NightFee;

        return total;
    }
}
