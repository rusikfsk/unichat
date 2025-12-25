using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly UniChatDbContext _db;

    public HealthController(UniChatDbContext db)
    {
        _db = db;
    }

    // GET /api/health
    // 200 = ok, 503 = проблемы (например БД недоступна)
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            // быстрый ping БД
            var ok = await _db.Database.CanConnectAsync(ct);
            if (!ok)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "unhealthy",
                    db = "cannot_connect"
                });

            
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);

            return Ok(new
            {
                status = "healthy",
                db = "ok",
                utc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                db = "error",
                error = ex.GetType().Name,
                message = ex.Message
            });
        }
    }
}

