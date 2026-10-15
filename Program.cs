using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Otopark.Data;
using Otopark.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Default")));
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<PricingService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<ILprService, SimulatedLprService>(); // Gerçek LPR için burayı değiştirin

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = cfg["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = cfg["Jwt:Issuer"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();


var app = builder.Build();

// Veritabanı hazırlığı: bağlantı sorunu olursa anlaşılır bir mesaj yaz
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (cfg.GetValue<bool>("ResetDatabase")) db.Database.EnsureDeleted(); // şemayı sıfırlar, yalnızca bir kez true yapın
    db.Database.EnsureCreated();
    DbSeeder.Seed(db, cfg.GetValue<bool>("SeedDemoData"));
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex,
        "Veritabanı hazırlanamadı. appsettings.json içindeki ConnectionStrings:Default değerini kontrol edin. " +
        "Daha önce eski sürümle veritabanı oluşturduysanız ResetDatabase=true yapıp bir kez çalıştırın.");
    throw;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(a => a.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync("{\"message\":\"Beklenmeyen bir hata oluştu.\"}");
    }));
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
