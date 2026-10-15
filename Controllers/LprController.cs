using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otopark.Services;

namespace Otopark.Controllers;

[ApiController, Route("api/lpr"), Authorize]
public class LprController(ILprService lpr) : ControllerBase
{
    [HttpPost("read")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Read([FromForm] IFormFile image)
    {
        if (image == null || image.Length == 0) return BadRequest(new { message = "Görüntü gerekli." });
        if (!(image.ContentType?.StartsWith("image/") ?? false)) return BadRequest(new { message = "Yalnızca görüntü dosyası yükleyin." });
        await using var s = image.OpenReadStream();
        return Ok(await lpr.ReadAsync(s, image.FileName));
    }
}
