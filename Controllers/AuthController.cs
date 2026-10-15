using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otopark.Data;
using Otopark.Services;

namespace Otopark.Controllers;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string OldPassword, string NewPassword);

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Username == req.Username && x.IsActive);
        if (u == null || !PasswordHasher.Verify(req.Password ?? "", u.PasswordHash))
            return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." });
        return Ok(new { token = tokens.Create(u), fullName = u.FullName, role = u.Role });
    }

    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var u = await db.Users.FindAsync(id);
        if (u == null || !PasswordHasher.Verify(req.OldPassword ?? "", u.PasswordHash))
            return BadRequest(new { message = "Mevcut şifre hatalı." });
        if ((req.NewPassword?.Length ?? 0) < 6)
            return BadRequest(new { message = "Yeni şifre en az 6 karakter olmalı." });

        u.PasswordHash = PasswordHasher.Hash(req.NewPassword!);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
