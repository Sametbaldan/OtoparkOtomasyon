using Otopark.Models;
using Otopark.Services;

namespace Otopark.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db, bool demo)
    {
        if (db.Users.Any()) return;

        db.Users.AddRange(
            new AppUser { Username = "admin", PasswordHash = PasswordHasher.Hash("Admin123!"), FullName = "Sistem Yöneticisi", Role = Roles.Admin },
            new AppUser { Username = "personel", PasswordHash = PasswordHasher.Hash("Personel123!"), FullName = "Vezne Görevlisi", Role = Roles.Personel });

        db.Tariffs.AddRange(
            new Tariff { VehicleType = VehicleType.Otomobil,   FreeMinutes = 15, FirstHourFee = 50, NextHourFee = 30, DailyMaxFee = 250, NightFee = 120, NightStartHour = 20, NightEndHour = 8 },
            new Tariff { VehicleType = VehicleType.Motosiklet, FreeMinutes = 15, FirstHourFee = 25, NextHourFee = 15, DailyMaxFee = 120, NightFee = 60,  NightStartHour = 20, NightEndHour = 8 },
            new Tariff { VehicleType = VehicleType.Minibus,    FreeMinutes = 15, FirstHourFee = 80, NextHourFee = 50, DailyMaxFee = 400, NightFee = 200, NightStartHour = 20, NightEndHour = 8 });

        for (int i = 1; i <= 40; i++) db.Slots.Add(new ParkingSlot { Code = $"A{i:00}" });
        db.SaveChanges();

        if (demo) SeedDemo(db);
    }

    // Grafikler boş görünmesin diye son 30 güne ait örnek kayıtlar
    private static void SeedDemo(AppDbContext db)
    {
        var rnd = new Random(42);
        var tariffs = db.Tariffs.ToDictionary(t => t.VehicleType);
        var adminId = db.Users.First().Id;
        var types = new[] { VehicleType.Otomobil, VehicleType.Otomobil, VehicleType.Otomobil, VehicleType.Motosiklet, VehicleType.Minibus };
        const string L = "ABCDEFGHJKLMNPRSTUVYZ";

        for (int d = 30; d >= 1; d--)
        {
            int n = rnd.Next(8, 22);
            for (int i = 0; i < n; i++)
            {
                var day = DateTime.Today.AddDays(-d);
                int hour = rnd.Next(100) < 50 ? rnd.Next(7, 10) : rnd.Next(100) < 60 ? rnd.Next(16, 20) : rnd.Next(0, 24);
                var entry = day.AddHours(hour).AddMinutes(rnd.Next(60));
                var exit = entry.AddMinutes(rnd.Next(10, 600));
                var vt = types[rnd.Next(types.Length)];
                var plate = $"{rnd.Next(1, 82):00}{L[rnd.Next(L.Length)]}{L[rnd.Next(L.Length)]}{rnd.Next(100, 1000)}";
                var fee = PricingService.Compute(tariffs[vt], entry, exit);
                db.ParkingRecords.Add(new ParkingRecord
                {
                    Plate = plate, VehicleType = vt, EntryTime = entry, ExitTime = exit,
                    TicketCode = Guid.NewGuid().ToString("N")[..10].ToUpper(), Fee = fee, CreatedByUserId = adminId
                });
                if (fee > 0)
                    db.Payments.Add(new Payment
                    {
                        Date = exit, Amount = fee, Kind = PaymentKind.Park, UserId = adminId,
                        Type = rnd.Next(2) == 0 ? PaymentType.Nakit : PaymentType.KrediKarti
                    });
            }
        }
        db.SaveChanges();
    }
}
