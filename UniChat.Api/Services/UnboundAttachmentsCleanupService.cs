using Microsoft.EntityFrameworkCore;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Services;

public sealed class UnboundAttachmentsCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UnboundAttachmentsCleanupService> _logger;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    public UnboundAttachmentsCleanupService(IServiceScopeFactory scopeFactory, ILogger<UnboundAttachmentsCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unbound attachments cleanup failed.");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }

    private async Task CleanupOnce(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<UniChatDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var cutoff = DateTimeOffset.UtcNow - MaxAge;

        // берём список "старых" непривязанных вложений
        var old = await db.Attachments
            .Where(a => a.MessageId == null && a.CreatedAt < cutoff)
            .Select(a => new { a.Id, a.StoragePath })
            .ToListAsync(ct);

        if (old.Count == 0) return;

        // 1) сначала пытаемся удалить объекты из MinIO
        // (даже если не получилось — мы не должны оставлять мусор в БД навсегда)
        var deletedObjects = 0;
        var failedObjects = 0;

        foreach (var a in old)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(a.StoragePath))
                {
                    await storage.DeleteAsync(a.StoragePath, ct);
                    deletedObjects++;
                }
            }
            catch
            {
                failedObjects++;
            }
        }

        // 2) удаляем записи из БД
        var ids = old.Select(x => x.Id).ToList();
        var entities = await db.Attachments
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(ct);

        db.Attachments.RemoveRange(entities);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cleanup removed {Count} unbound attachments, deletedObjects={DeletedObjects}, failedObjects={FailedObjects}",
            old.Count,
            deletedObjects,
            failedObjects);
    }
}
