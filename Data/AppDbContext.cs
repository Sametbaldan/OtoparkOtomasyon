using Microsoft.EntityFrameworkCore;
using Otopark.Models;

namespace Otopark.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<ParkingSlot> Slots => Set<ParkingSlot>();
    public DbSet<ParkingRecord> ParkingRecords => Set<ParkingRecord>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(10, 2);
        b.Properties<VehicleType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<PaymentType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<PaymentKind>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        m.Entity<Tariff>().HasIndex(t => t.VehicleType).IsUnique();
        m.Entity<ParkingSlot>().HasIndex(s => s.Code).IsUnique();

        // Aynı plaka aynı anda iki kez içeride olamaz, aynı yer iki araca verilemez (veritabanı seviyesinde)
        m.Entity<ParkingRecord>().HasIndex(r => r.Plate).IsUnique().HasFilter("[ExitTime] IS NULL");
        m.Entity<ParkingRecord>().HasIndex(r => r.SlotId).IsUnique().HasFilter("[ExitTime] IS NULL AND [SlotId] IS NOT NULL");
        m.Entity<ParkingRecord>().HasIndex(r => r.TicketCode).IsUnique();
        m.Entity<ParkingRecord>().HasIndex(r => r.EntryTime);

        m.Entity<Subscription>().HasIndex(s => s.Plate);
        m.Entity<Payment>().HasIndex(p => p.Date);
    }
}
