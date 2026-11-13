using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Уведомления с приватной полезной нагрузкой и уникальным ключом доставки.</summary>
public static class CollaborationNotices
{
  /// <summary>Готовит уведомления только фактическим участникам области, а не всем администраторам.</summary>
  public static async Task Add(ApplicationDbContext db, ChatMessage message, ChatRoom room, string kind, string key)
  {
    var users = await db.Users.Where(x => !x.MustChangePassword && (x.Id != message.AuthorId || kind == "assigned" || kind == "cancelled" || kind == "reminder")).ToListAsync();
    var prefs = await db.Set<ChatPreference>().ToListAsync();
    foreach (var user in users)
    {
      if (room.Readers.Length > 0 && !room.Readers.Contains(user.Id)) continue;
      if (message.Readers.Length > 0 && !message.Readers.Contains(user.Id)) continue;
      var actualKind = kind == "message" ? message.Readers.Length > 0 ? "private" : room.IsThread ? "thread" : "message" : kind;
      // Явные упоминания по отображаемому имени не расширяют область доступа.
      if (kind == "message" && !string.IsNullOrEmpty(user.DisplayName) && message.Text.Contains("@" + user.DisplayName, StringComparison.OrdinalIgnoreCase)) actualKind = "mention";
      var pref = prefs.Find(x => x.UserId == user.Id) ?? new();
      if (!pref.Events.Contains(actualKind)) continue;
      if (await db.Set<ChatNotice>().AnyAsync(x => x.UserId == user.Id && x.EventKey == key)) continue;
      db.Add(new ChatNotice { UserId = user.Id, RoomId = room.Id, MessageId = message.Id,
        Kind = actualKind, EventKey = key, Title = actualKind switch
        {
          "private" => "Новое личное сообщение", "thread" => "Ответ в ветке", "mention" => "Вас упомянули",
          "poll" => "Новый опрос", "request" => "Проверьте свои календари", "assigned" => "Игра назначена",
          "cancelled" => "Игра отменена", "reminder" => "Скоро игра", _ => "Новое сообщение"
        } });
    }
  }
}
