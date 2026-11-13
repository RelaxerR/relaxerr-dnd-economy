using System.Security.Claims;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Domain.Constants;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Проверенный по текущей БД пользователь, а не доверенный клиентский идентификатор или старая роль cookie.</summary>
public sealed record ChatActor(Guid Id, bool Admin);

/// <summary>Единая область доступа для запросов, файлов, статусов и фоновой доставки.</summary>
public static class ChatAccess
{
  /// <summary>Повторно проверяет Identity при каждом действии и переподключении.</summary>
  public static async Task<ChatActor> Authenticate(ApplicationDbContext db, ClaimsPrincipal principal)
  {
    if (principal.Identity?.IsAuthenticated != true || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
      throw new UnauthorizedAccessException();
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    if (user is null || user.MustChangePassword || user.LockoutEnd > DateTimeOffset.UtcNow)
      throw new UnauthorizedAccessException();
    var stamp = principal.FindFirstValue("AspNet.Identity.SecurityStamp");
    if (stamp is null || stamp != user.SecurityStamp) throw new UnauthorizedAccessException();
    var admin = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
      where ur.UserId == id && r.Name == RoleNames.Admin select ur).AnyAsync();
    return new(id, admin);
  }

  /// <summary>Фильтр комнат не выдаёт даже метаданные личной ветки постороннему.</summary>
  public static IQueryable<ChatRoom> Rooms(ApplicationDbContext db, ChatActor a) =>
    db.Set<ChatRoom>().Where(x => a.Admin || x.Readers.Length == 0 || x.Readers.Contains(a.Id));

  /// <summary>Фильтр сообщений всегда включает обе области: комнату и сообщение.</summary>
  public static IQueryable<ChatMessage> Messages(ApplicationDbContext db, ChatActor a) =>
    db.Set<ChatMessage>().Where(x => Rooms(db, a).Any(r => r.Id == x.RoomId) &&
      (a.Admin || x.Readers.Length == 0 || x.AuthorId == a.Id || x.Readers.Contains(a.Id)));

  /// <summary>Закрытый отказ одинаков для отсутствующего и недоступного ресурса.</summary>
  public static async Task<ChatRoom> Room(ApplicationDbContext db, ChatActor a, Guid id) =>
    await Rooms(db, a).SingleOrDefaultAsync(x => x.Id == id) ?? throw new UnauthorizedAccessException();

  /// <summary>Закрытый отказ для прямых ссылок на сообщения.</summary>
  public static async Task<ChatMessage> Message(ApplicationDbContext db, ChatActor a, Guid id) =>
    await Messages(db, a).SingleOrDefaultAsync(x => x.Id == id) ?? throw new UnauthorizedAccessException();

  /// <summary>Последовательные изменения модуля сериализуются между процессами PostgreSQL до commit.</summary>
  public static Task Lock(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(72418026)");
}
