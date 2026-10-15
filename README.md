# Otopark Takip Otomasyonu

ASP.NET Core 10 (.NET 10) Web API + EF Core + SQL Server + Bootstrap web arayüzü.

## Çalıştırma

### Visual Studio 2022
1. Zip'i **Tümünü ayıkla** ile bir klasöre çıkarın.
2. **Dosya → Aç → Proje/Çözüm** ile `OtoparkOtomasyon.sln` dosyasını seçin ("Klasör aç" kullanmayın).
3. Üstte `OtoparkOtomasyon` profili seçiliyken **F5**.
   (Gerekli: Visual Studio 2022 17.14+ veya Visual Studio 2026, "ASP.NET ve web geliştirme" iş yükü, .NET 10 SDK.)

### Komut satırı
```
dotnet restore
dotnet run --urls http://localhost:5080
```
Tarayıcıda `http://localhost:5080`.

### Ayarlar (`appsettings.json`)
- `ConnectionStrings:Default`: SQL Server bağlantısı. LocalDB için varsayılan çalışır.
  SQL Express: `Server=.\\SQLEXPRESS;Database=OtoparkDb;Trusted_Connection=True;TrustServerCertificate=True`
- `Jwt:Key`: canlıda uzun, rastgele bir anahtarla değiştirin.
- `SeedDemoData`: `true` ise son 30 güne ait örnek kayıtlar eklenir (canlıda `false`).
- `ResetDatabase`: `true` yapıp bir kez çalıştırırsanız veritabanı silinip yeniden oluşturulur.
  **Eski sürümle oluşturulmuş bir `OtoparkDb` varsa bunu bir kez yapın**, sonra tekrar `false` yapın.

## Varsayılan kullanıcılar (ilk girişte "Şifre değiştir" ile değiştirin)
| Kullanıcı | Şifre | Rol |
|---|---|---|
| admin | Admin123! | Admin |
| personel | Personel123! | Personel |

## Özellikler
- Giriş/çıkış, otomatik yer ataması, kalış süresi ve ücret hesabı, makbuz/fiş yazdırma
- Tarife: ücretsiz süre, ilk saat, sonraki saat, günlük tavan, gece tarifesi (araç türüne göre)
- Doluluk şeması (15 sn'de bir yenilenir); admin yer ekleyebilir, yerleri açıp kapatabilir
- Geçmiş: plaka arama, CSV (Excel) dışa aktarma
- Kasa: günlük/haftalık/aylık gelir, nakit/kart ve park/abonelik kırılımı
- LPR: fotoğraftan plaka okuma (simülasyon; dosya adı `34ABC123.jpg` gibiyse onu okur)
- Abonelik: tanımlama, yenileme, 7 gün kala uyarı bandı; abone araç çıkışta ücretsiz
- QR fiş + herkese açık dijital fiş sayfası (`/ticket.html?code=...`, plaka maskelenir)
- Analitik: en yoğun saat, gün dağılımı, araç türü dağılımı, ortalama kalış
- RBAC: Admin (tarife, rapor, abonelik, personel, yer yönetimi) / Personel (giriş, çıkış, ödeme)
- Güvenlik/sağlamlık: PBKDF2 şifre, JWT, XSS kaçışı, aynı plaka/aynı yer için veritabanı kilidi,
  çift çıkış/çift ödeme koruması

## Gerçek sisteme geçerken
- LPR: `Services/LprService.cs` içindeki `ILprService`'i gerçek bir servisle (Plate Recognizer,
  Azure AI Vision vb.) uygulayıp `Program.cs`'te kaydı değiştirin.
- Bariyer: `ParkingController` `Entry`/`Exit` sonuna bariyer kontrolcüsü çağrısı ekleyin.
- SMS/E-posta ile dijital fiş: `ticket.html?code=` bağlantısını bir SMS sağlayıcısıyla gönderin.
- Üretimde `EnsureCreated` yerine EF Core migration kullanın, HTTPS zorunlu tutun.
