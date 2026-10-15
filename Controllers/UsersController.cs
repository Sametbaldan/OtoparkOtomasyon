using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Models;
using Otopark.Services;

namespace Otopark.Controllers;

public record UserCreate(string Username, string Password, string FullName, string Role);

[ApiController, Route("api/users"), Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await db.Users.OrderBy(u => u.Id).Select(u => new { u.Id, u.Username, u.FullName, u.Role, u.IsActive }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(UserCreate req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || req.Password?.Length < 6)
            return BadRequest(new { message = "Kullanıcı adı gerekli, şifre en az 6 karakter olmalı." });
        if (req.Role != Roles.Admin && req.Role != Roles.Personel)
            return BadRequest(new { message = "Rol Admin veya Personel olmalı." });
        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { message = "Bu kullanıcı adı kullanılıyor." });

        var u = new AppUser
        {
            Username = req.Username.Trim(), FullName = req.FullName?.Trim() ?? "", Role = req.Role,
            PasswordHash = PasswordHasher.Hash(req.Password!)
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { u.Id, u.Username, u.FullName, u.Role, u.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return BadRequest(new { message = "Kendi hesabınızı pasifleştiremezsiniz." });
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        u.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
