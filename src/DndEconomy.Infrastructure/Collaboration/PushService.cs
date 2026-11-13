using System.Net;
using System.Security.Claims;
using System.Text.Json;
using DndEconomy.Application.Collaboration;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebPush;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Регистрация и удаление подписки с проверкой Identity и поставщика push.</summary>
public sealed class PushService(IDbContextFactory<ApplicationDbContext> factory, IConfiguration config) : IPushService
{
  public string? PublicKey => config["WebPush:PublicKey"];

  /// <summary>Разрешённые браузерные push-службы; произвольные URL не отправляются сервером.</summary>
  public static bool AllowedEndpoint(string endpoint)
  {
    if (endpoint.Length > 2048 || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo != "" || uri.Fragment != "") return false;
    var host = uri.IdnHost;
    return host is "fcm.googleapis.com" or "updates.push.services.mozilla.com" or "web.push.apple.com" ||
      host.EndsWith(".push.apple.com", StringComparison.Ordinal) || host.EndsWith(".notify.windows.com", StringComparison.Ordinal);
  }

  /// <inheritdoc />
  public async Task Subscribe(ClaimsPrincipal user, string endpoint, string p256dh, string auth)
  {
    if (string.IsNullOrEmpty(PublicKey) || string.IsNullOrEmpty(config["WebPush:PrivateKey"])) throw new ArgumentException("Web Push ещё не настроен администратором.");
    if (!AllowedEndpoint(endpoint) || !Key(p256dh, 65) || !Key(auth, 16)) throw new ArgumentException("Некорректная подписка устройства.");
    await using var db = await factory.CreateDbContextAsync(); await using var tx = await db.Database.BeginTransactionAsync();
    await ChatAccess.Lock(db); var a = await ChatAccess.Authenticate(db, user);
    var subscription = await db.Set<ChatPushSubscription>().SingleOrDefaultAsync(x => x.Endpoint == endpoint);
    if (subscription is not null && subscription.UserId != a.Id) throw new ArgumentException("Отключите подписку прежнего аккаунта на этом устройстве и подключите заново.");
    if (subscription is null)
    {
      if (await db.Set<ChatPushSubscription>().CountAsync(x => x.UserId == a.Id) >= 20) throw new ArgumentException("Достигнут лимит устройств.");
      subscription = new() { UserId = a.Id, Endpoint = endpoint }; db.Add(subscription);
    }
    subscription.SecurityStamp = (await db.Users.SingleAsync(x => x.Id == a.Id)).SecurityStamp!;
    subscription.P256dh = p256dh; subscription.Auth = auth;
    await db.SaveChangesAsync(); await tx.CommitAsync();
  }

  private static bool Key(string key, int length)
  {
    if (key.Length > 128) return false;
    try { return Convert.FromBase64String(key.Replace('-', '+').Replace('_', '/').PadRight((key.Length + 3) / 4 * 4, '=')).Length == length; }
    catch (FormatException) { return false; }
  }

  /// <inheritdoc />
  public async Task Unsubscribe(ClaimsPrincipal user, string endpoint)
  {
    await using var db = await factory.CreateDbContextAsync(); await using var tx = await db.Database.BeginTransactionAsync();
    await ChatAccess.Lock(db); var a = await ChatAccess.Authenticate(db, user);
    await db.Set<ChatPushSubscription>().Where(x => x.UserId == a.Id && x.Endpoint == endpoint).ExecuteDeleteAsync();
    await tx.CommitAsync();
  }
}

/// <summary>Доставка очереди с повторной проверкой прав непосредственно перед HTTP-запросом.</summary>
public sealed class ChatPushWorker(IDbContextFactory<ApplicationDbContext> factory, IConfiguration config,
  TimeProvider clock, ILogger<ChatPushWorker> logger) : BackgroundService
{
  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    using var client = new WebPushClient(http);
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        if (!string.IsNullOrEmpty(config["WebPush:PrivateKey"])) await Deliver(client, stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      catch (Exception e) { logger.LogWarning("Ошибка фоновой доставки push: {Type}", e.GetType().Name); }
      await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
    }
  }

  /// <summary>Доставляет одну пачку; HTTP-клиент подменяется в тестах без реальных push-служб.</summary>
  public async Task Deliver(WebPushClient client, CancellationToken ct)
  {
    var now = clock.GetUtcNow().UtcDateTime;
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(72418027)", ct);
    var pending = await (from n in db.Set<ChatNotice>() join s in db.Set<ChatPushSubscription>() on n.UserId equals s.UserId
      where n.CreatedUtc >= s.CreatedUtc && n.CreatedUtc > now.AddDays(-2) && !n.Read &&
        !db.Set<ChatPushDelivery>().Any(d => d.NoticeId == n.Id && d.SubscriptionId == s.Id && (d.SentUtc != null || d.NextAttemptUtc > now))
      orderby n.CreatedUtc select new { Notice = n, Subscription = s }).Take(20).ToListAsync(ct);
    foreach (var item in pending)
    {
      var n = item.Notice; var s = item.Subscription;
      if (db.Entry(s).State == EntityState.Deleted) continue;
      var delivery = await db.Set<ChatPushDelivery>().FindAsync([n.Id, s.Id], ct);
      if (delivery is null) { delivery = new() { NoticeId = n.Id, SubscriptionId = s.Id }; db.Add(delivery); }
      var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == n.UserId, ct);
      if (user is null || user.SecurityStamp != s.SecurityStamp || user.MustChangePassword || user.LockoutEnd > DateTimeOffset.UtcNow || !PushService.AllowedEndpoint(s.Endpoint)) { delivery.SentUtc = now; continue; }
      var admin = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == user.Id && r.Name == "Admin" select ur).AnyAsync(ct);
      var a = new ChatActor(user.Id, admin);
      var pref = await db.Set<ChatPreference>().FindAsync([user.Id], ct) ?? new();
      if (!pref.Events.Contains(n.Kind) || !await ChatAccess.Rooms(db, a).AnyAsync(x => x.Id == n.RoomId, ct) ||
          n.MessageId is { } mid && !await ChatAccess.Messages(db, a).AnyAsync(x => x.Id == mid, ct)) { delivery.SentUtc = now; continue; }
      try
      {
        var payload = JsonSerializer.Serialize(new { title = n.Title, tag = n.Id.ToString(),
          url = n.MessageId is { } m ? $"/chat?message={m}" : $"/chat?room={n.RoomId}" });
        await client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload,
          new VapidDetails(config["WebPush:Subject"], config["WebPush:PublicKey"], config["WebPush:PrivateKey"]), ct);
        delivery.SentUtc = now;
      }
      catch (WebPushException e) when (e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
      {
        db.Remove(s); delivery.SentUtc = now;
      }
      catch (Exception e) when (e is WebPushException or HttpRequestException or TaskCanceledException)
      {
        delivery.Attempts++; delivery.NextAttemptUtc = now.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(6, delivery.Attempts))));
        logger.LogWarning("Push не доставлен; попытка {Attempt}, тип {Type}", delivery.Attempts, e.GetType().Name);
      }
    }
    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
  }
}
