using System.ComponentModel.DataAnnotations;

namespace Otopark.Models;

public enum VehicleType { Otomobil, Motosiklet, Minibus }
public enum PaymentType { Nakit, KrediKarti }
public enum PaymentKind { Park, Abonelik }

public static class Roles
{
    public const string Admin = "Admin";
    public const string Personel = "Personel";
}

public class AppUser
{
    public int Id { get; set; }
    [MaxLength(50)] public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    [MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(20)] public string Role { get; set; } = Roles.Personel;
    public bool IsActive { get; set; } = true;
}

public class Tariff
{
    public int Id { get; set; }
    public VehicleType VehicleType { get; set; }
    public int FreeMinutes { get; set; } = 15;        // ücretsiz bekleme süresi
    public decimal FirstHourFee { get; set; }          // ilk saat
    public decimal NextHourFee { get; set; }           // sonraki her saat
    public decimal DailyMaxFee { get; set; }           // 24 saatlik tavan (tam gün)
    public decimal NightFee { get; set; }              // gece tarifesi (0 = kapalı)
    public int NightStartHour { get; set; } = 20;
    public int NightEndHour { get; set; } = 8;
}

public class ParkingSlot
{
    public int Id { get; set; }
    [MaxLength(10)] public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class ParkingRecord
{
    public int Id { get; set; }
    [MaxLength(12)] public string Plate { get; set; } = "";
    public VehicleType VehicleType { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public int? SlotId { get; set; }
    public ParkingSlot? Slot { get; set; }
    [MaxLength(12)] public string TicketCode { get; set; } = "";
    public decimal Fee { get; set; }
    public bool IsSubscriber { get; set; }
    public int CreatedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class Subscription
{
    public int Id { get; set; }
    [MaxLength(12)] public string Plate { get; set; } = "";
    [MaxLength(100)] public string OwnerName { get; set; } = "";
    [MaxLength(20)] public string? Phone { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Payment
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentType Type { get; set; }
    public PaymentKind Kind { get; set; }
    public int? RecordId { get; set; }
    public int UserId { get; set; }
}
