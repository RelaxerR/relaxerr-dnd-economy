using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Возобновляемая уборка, переходы планирования и напоминания без памяти о предыдущем запуске.</summary>
public sealed class CollaborationWorker(IDbContextFactory<ApplicationDbContext> factory, IConfiguration config,
  TimeProvider clock, ILogger<CollaborationWorker> logger) : BackgroundService
{
  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try { await Tick(stoppingToken); }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      catch (Exception e) { logger.LogError(e, "Не удалось обработать фоновые задачи чата; следующая попытка через минуту"); }
      await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
  }

  /// <summary>Один идемпотентный проход; доступен интеграционным проверкам с тестовым временем.</summary>
  public async Task Tick(CancellationToken ct)
  {
    var now = clock.GetUtcNow().UtcDateTime;
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await ChatAccess.Lock(db);
    await SchedulingService.Recalculate(db, now);
    var games = await db.Set<ScheduledGame>().Where(x => x.CancelledUtc == null && x.StartUtc > now && x.StartUtc <= now.AddHours(24)).ToListAsync(ct);
    foreach (var game in games)
    foreach (var hours in new[] { 24, 1 }.Where(h => game.StartUtc <= now.AddHours(h)))
    {
      var r = await db.Set<SchedulingRequest>().SingleAsync(x => x.Id == game.RequestId, ct);
      var card = await db.Set<ChatMessage>().SingleAsync(x => x.Id == r.CardMessageId, ct);
      var room = await db.Set<ChatRoom>().SingleAsync(x => x.Id == r.RoomId, ct);
      await CollaborationNotices.Add(db, card, room, "reminder", $"reminder:{game.Id}:{hours}");
    }
    var root = Path.GetFullPath(config["Chat:StoragePath"] ?? "private-chat-files");
    var expired = await db.Set<ChatAttachment>().Where(x => x.ExpiresUtc <= now).ToListAsync(ct);
    foreach (var file in expired)
    {
      File.Delete(Path.Combine(root, file.Id.ToString("N")));
      db.Remove(file);
    }
    if (Directory.Exists(root))
    {
      var live = (await db.Set<ChatAttachment>().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
      foreach (var path in Directory.EnumerateFiles(root))
        if (Guid.TryParseExact(Path.GetFileName(path), "N", out var id) && !live.Contains(id) && File.GetLastWriteTimeUtc(path) < now.AddDays(-1)) File.Delete(path);
    }
    await db.Set<ChatPresence>().Where(x => x.SeenUtc < now.AddMinutes(-5)).ExecuteDeleteAsync(ct);
    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
  }
}
