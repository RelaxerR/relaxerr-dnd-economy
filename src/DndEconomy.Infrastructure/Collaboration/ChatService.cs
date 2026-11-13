using System.Security.Claims;
using System.Text.RegularExpressions;
using DndEconomy.Application.Collaboration;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Чат кампании; краткоживущие контексты безопасны для параллельных Blazor circuits.</summary>
public sealed partial class ChatService(IDbContextFactory<ApplicationDbContext> factory, IConfiguration config,
  TimeProvider clock) : IChatService
{
  private DateTime Now => clock.GetUtcNow().UtcDateTime;
  private string Root => Path.GetFullPath(config["Chat:StoragePath"] ?? "private-chat-files");
  public const long MaxFileBytes = 100_000_000;

  private async Task<T> Write<T>(ClaimsPrincipal user, Func<ApplicationDbContext, ChatActor, Task<T>> action)
  {
    await using var db = await factory.CreateDbContextAsync();
    await using var tx = await db.Database.BeginTransactionAsync();
    await ChatAccess.Lock(db);
    var actor = await ChatAccess.Authenticate(db, user);
    var result = await action(db, actor);
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return result;
  }

  internal static string Text(string text, int max = 12000)
  {
    text = text.Trim();
    if (text.Length == 0 || text.Length > max) throw new ArgumentException($"Введите текст длиной 1–{max} символов.");
    return text;
  }

  /// <inheritdoc />
  public async Task<(Guid Room, int Take)> Locate(ClaimsPrincipal user, Guid message)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    var m = await ChatAccess.Message(db, a, message);
    var count = await ChatAccess.Messages(db, a).CountAsync(x => x.RoomId == m.RoomId && x.CreatedUtc >= m.CreatedUtc);
    return (m.RoomId, count + 50);
  }

  /// <inheritdoc />
  public Task<Guid> Main(ClaimsPrincipal user) => Write(user, async (db, a) =>
  {
    var room = await db.Set<ChatRoom>().SingleOrDefaultAsync(x => x.IsMain);
    if (room is null) { room = new() { Name = "Главный чат", IsMain = true, ActivityUtc = Now }; db.Add(room); }
    return room.Id;
  });

  /// <inheritdoc />
  public async Task<ChatSnapshot> Snapshot(ClaimsPrincipal user, Guid room, int take, bool archive)
  {
    await using var db = await factory.CreateDbContextAsync();
    var a = await ChatAccess.Authenticate(db, user);
    var currentRoom = await ChatAccess.Room(db, a, room);
    var cutoff = Now.AddDays(-7);
    var rooms = await ChatAccess.Rooms(db, a).AsNoTracking()
      .Where(x => !x.IsThread || (archive ? x.ActivityUtc <= cutoff : x.ActivityUtc > cutoff))
      .OrderByDescending(x => x.IsMain).ThenByDescending(x => x.ActivityUtc).ToListAsync();
    var roomIds = rooms.Select(x => x.Id).ToArray();
    var unread = await ChatAccess.Messages(db, a).Where(x => roomIds.Contains(x.RoomId) && x.AuthorId != a.Id &&
      !db.Set<ChatRead>().Any(r => r.MessageId == x.Id && r.UserId == a.Id))
      .GroupBy(x => x.RoomId).Select(g => new { Room = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Room, x => x.Count);
    var messages = await ChatAccess.Messages(db, a).AsNoTracking().Where(x => x.RoomId == room)
      .OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).Take(Math.Max(take, 1)).ToListAsync();
    var pinned = await ChatAccess.Messages(db, a).AsNoTracking().Where(x => x.RoomId == room && x.Highlighted).OrderBy(x => x.CreatedUtc).ToListAsync();
    var ids = messages.Select(x => x.Id).ToArray();
    var reads = await db.Set<ChatRead>().AsNoTracking().Where(x => ids.Contains(x.MessageId)).ToListAsync();
    var files = await db.Set<ChatAttachment>().AsNoTracking().Where(x => ids.Contains(x.MessageId) && x.ExpiresUtc > Now).ToListAsync();
    var polls = await db.Set<ChatPoll>().AsNoTracking().Where(x => ids.Contains(x.MessageId)).ToListAsync();
    var pollIds = polls.Select(x => x.Id).ToArray();
    var options = await db.Set<PollOption>().AsNoTracking().Where(x => pollIds.Contains(x.PollId)).ToListAsync();
    var optionIds = options.Select(x => x.Id).ToArray();
    var votes = await db.Set<PollVote>().AsNoTracking().Where(x => optionIds.Contains(x.OptionId)).ToListAsync();
    var preferences = await db.Set<ChatPreference>().AsNoTracking().ToListAsync();
    var users = await db.Users.AsNoTracking().Where(x => !x.MustChangePassword && (x.LockoutEnd == null || x.LockoutEnd <= DateTimeOffset.UtcNow)).ToListAsync();
    var people = users.Select(x => new ChatPerson(x.Id, x.DisplayName, preferences.Find(p => p.UserId == x.Id)?.Color ?? "#8b4513")).ToList();
    // Онлайн — общий статус аккаунта, «печатает» — только в доступной комнате, без адресатов/текста черновика.
    var presence = await db.Set<ChatPresence>().AsNoTracking().Where(x => x.SeenUtc > Now.AddSeconds(-45)).ToListAsync();
    var requests = await db.Set<SchedulingRequest>().AsNoTracking().Where(x => x.RoomId == room).ToListAsync();
    var requestIds = requests.Select(x => x.Id).ToArray();
    var games = await db.Set<ScheduledGame>().AsNoTracking().Where(x => requestIds.Contains(x.RequestId)).ToListAsync();
    var ballots = await db.Set<SchedulingBallot>().AsNoTracking().Where(x => requestIds.Contains(x.RequestId)).ToListAsync();
    return new(currentRoom, rooms, messages.OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).Select(m =>
    {
      var poll = polls.Find(p => p.MessageId == m.Id);
      var opts = options.Where(x => x.PollId == poll?.Id).ToList();
      return new MessageView(m, reads.Where(x => x.MessageId == m.Id).Select(x => x.UserId).ToList(),
        files.Where(x => x.MessageId == m.Id).ToList(), poll, opts, votes.Where(x => opts.Any(o => o.Id == x.OptionId)).ToList());
    }).ToList(), pinned, people, presence.Select(x => x.UserId).Distinct().ToList(),
      presence.Where(x => x.RoomId == room && x.TypingUntilUtc > Now && x.UserId != a.Id).Select(x => x.UserId).Distinct().ToList(), requests, games, ballots) { Unread = unread };
  }

  /// <inheritdoc />
  public async Task<List<ChatMessage>> Search(ClaimsPrincipal user, string query)
  {
    await using var db = await factory.CreateDbContextAsync();
    var a = await ChatAccess.Authenticate(db, user);
    query = Text(query, 200);
    return await ChatAccess.Messages(db, a).AsNoTracking().Where(x => x.Text.ToLower().Contains(query.ToLower()))
      .OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync();
  }

  /// <inheritdoc />
  public Task<Guid> CreateRoom(ClaimsPrincipal user, string name) => Write(user, (db, a) =>
  {
    if (!a.Admin) throw new UnauthorizedAccessException();
    var room = new ChatRoom { Name = Text(name, 200), ActivityUtc = Now };
    db.Add(room); return Task.FromResult(room.Id);
  });

  /// <inheritdoc />
  public Task<Guid> Thread(ClaimsPrincipal user, Guid message) => Write(user, async (db, a) =>
  {
    var root = await ChatAccess.Message(db, a, message);
    var existing = await db.Set<ChatRoom>().SingleOrDefaultAsync(x => x.ParentMessageId == message);
    if (existing is not null) return (await ChatAccess.Room(db, a, existing.Id)).Id;
    var parent = await ChatAccess.Room(db, a, root.RoomId);
    // Вложенная ветка не расширяет область даже при публичном ответе внутри личной ветки.
    var readers = root.Readers.Length > 0 ? root.Readers.Append(root.AuthorId).Distinct().ToArray() : parent.Readers;
    if (parent.Readers.Length > 0) readers = readers.Intersect(parent.Readers).ToArray();
    var thread = new ChatRoom { Name = "Ветка сообщения", IsThread = true, ParentMessageId = message, Readers = readers, ActivityUtc = Now };
    db.Add(thread); return thread.Id;
  });

  internal static async Task<Guid[]> Recipients(ApplicationDbContext db, ChatRoom room, ChatActor a, Guid[] readers)
  {
    readers = readers.Distinct().ToArray();
    if (readers.Length > 100) throw new ArgumentException("Слишком много адресатов.");
    var valid = await db.Users.Where(x => readers.Contains(x.Id) && !x.MustChangePassword).Select(x => x.Id).ToListAsync();
    if (valid.Count != readers.Length || room.Readers.Length > 0 && readers.Any(x => !room.Readers.Contains(x)))
      throw new ArgumentException("Адресат недоступен в этой ветке.");
    return readers.Length > 0 ? readers.Append(a.Id).Distinct().ToArray() : [];
  }

  /// <inheritdoc />
  public Task<Guid> Send(ClaimsPrincipal user, Guid room, string text, Guid[] readers, DateTime? confirmedUtc = null, string? zone = null) => Write(user, async (db, a) =>
  {
    var target = await ChatAccess.Room(db, a, room);
    var m = new ChatMessage { RoomId = room, AuthorId = a.Id, Readers = await Recipients(db, target, a, readers), Text = Text(text), CreatedUtc = Now };
    if (confirmedUtc is not null)
    {
      if (confirmedUtc.Value.Kind != DateTimeKind.Utc) throw new ArgumentException("Ожидается UTC.");
      _ = TimeZoneInfo.FindSystemTimeZoneById(zone ?? "");
      m.ConfirmedTimeUtc = confirmedUtc; m.SourceZone = zone;
    }
    db.Add(m); target.ActivityUtc = Now;
    await CollaborationNotices.Add(db, m, target, "message", $"message:{m.Id}");
    return m.Id;
  });

  /// <inheritdoc />
  public Task Edit(ClaimsPrincipal user, Guid message, string text) => Write(user, async (db, a) =>
  {
    var m = await ChatAccess.Message(db, a, message);
    if (m.AuthorId != a.Id) throw new UnauthorizedAccessException();
    m.Text = Text(text); m.EditedUtc = Now; m.ConfirmedTimeUtc = null; m.SourceZone = null;
    return true;
  });

  /// <inheritdoc />
  public Task Delete(ClaimsPrincipal user, Guid message) => Write(user, async (db, a) =>
  {
    var m = await ChatAccess.Message(db, a, message);
    if (m.AuthorId != a.Id && !a.Admin) throw new UnauthorizedAccessException();
    // Карточки планирования принадлежат workflow; произвольное удаление сломало бы его историю.
    if (await db.Set<SchedulingRequest>().AnyAsync(x => x.CardMessageId == message)) throw new ArgumentException("Карточка активного процесса не удаляется.");
    db.Remove(m); return true;
  });

  /// <inheritdoc />
  public Task Highlight(ClaimsPrincipal user, Guid message) => Write(user, async (db, a) =>
  {
    if (!a.Admin) throw new UnauthorizedAccessException();
    var m = await ChatAccess.Message(db, a, message); m.Highlighted = !m.Highlighted; return true;
  });

  /// <inheritdoc />
  public Task Read(ClaimsPrincipal user, Guid room, Guid[] messages) => Write(user, async (db, a) =>
  {
    await ChatAccess.Room(db, a, room);
    var visible = await ChatAccess.Messages(db, a).Where(x => x.RoomId == room && messages.Contains(x.Id)).Select(x => x.Id).ToListAsync();
    var existing = await db.Set<ChatRead>().Where(x => x.UserId == a.Id && visible.Contains(x.MessageId)).Select(x => x.MessageId).ToListAsync();
    await db.Set<ChatNotice>().Where(x => x.UserId == a.Id && x.MessageId != null && visible.Contains(x.MessageId.Value))
      .ExecuteUpdateAsync(s => s.SetProperty(x => x.Read, true));
    foreach (var id in visible.Except(existing)) db.Add(new ChatRead { MessageId = id, UserId = a.Id, ReadUtc = Now });
    return true;
  });

  /// <inheritdoc />
  public Task Presence(ClaimsPrincipal user, Guid room, string connection, bool typing) => Write(user, async (db, a) =>
  {
    await ChatAccess.Room(db, a, room);
    if (connection.Length > 100) throw new ArgumentException();
    var p = await db.Set<ChatPresence>().FindAsync(connection);
    if (p is not null && p.UserId != a.Id) throw new UnauthorizedAccessException();
    if (p is null) { p = new() { ConnectionId = connection, UserId = a.Id }; db.Add(p); }
    p.RoomId = room; p.SeenUtc = Now; p.TypingUntilUtc = typing ? Now.AddSeconds(5) : Now;
    return true;
  });

  /// <inheritdoc />
  public async Task<ChatPreference> Preference(ClaimsPrincipal user)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    return await db.Set<ChatPreference>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == a.Id) ?? new() { UserId = a.Id };
  }

  /// <inheritdoc />
  public Task SavePreference(ClaimsPrincipal user, ChatPreference input) => Write(user, async (db, a) =>
  {
    if (!Regex.IsMatch(input.Color, "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Нужен цвет #RRGGBB.");
    _ = TimeZoneInfo.FindSystemTimeZoneById(input.Zone);
    var p = await db.Set<ChatPreference>().FindAsync(a.Id);
    if (p is null) { p = new() { UserId = a.Id }; db.Add(p); }
    p.Color = input.Color; p.Zone = input.Zone; p.Events = input.Events.Distinct().Take(20).ToArray(); return true;
  });

  /// <inheritdoc />
  public Task CreatePoll(ClaimsPrincipal user, Guid room, string question, string[] options, bool multiple, DateTime endsUtc, Guid[] readers) => Write(user, async (db, a) =>
  {
    if (endsUtc.Kind != DateTimeKind.Utc || endsUtc <= Now || options.Length is < 2 or > 50) throw new ArgumentException("Нужны 2–50 вариантов и будущий срок окончания.");
    var target = await ChatAccess.Room(db, a, room);
    var m = new ChatMessage { RoomId = room, AuthorId = a.Id, Readers = await Recipients(db, target, a, readers), Text = Text(question), CreatedUtc = Now };
    var poll = new ChatPoll { MessageId = m.Id, Multiple = multiple, EndsUtc = endsUtc };
    db.Add(m); db.Add(poll);
    foreach (var option in options) db.Add(new PollOption { PollId = poll.Id, AuthorId = a.Id, Text = Text(option, 500) });
    target.ActivityUtc = Now;
    await CollaborationNotices.Add(db, m, target, "poll", $"poll:{poll.Id}"); return true;
  });

  private async Task<ChatPoll> Poll(ApplicationDbContext db, ChatActor a, Guid id, bool open = true)
  {
    var poll = await db.Set<ChatPoll>().SingleOrDefaultAsync(x => x.Id == id) ?? throw new UnauthorizedAccessException();
    await ChatAccess.Message(db, a, poll.MessageId);
    if (open && poll.EndsUtc <= Now) throw new ArgumentException("Опрос завершён.");
    return poll;
  }

  /// <inheritdoc />
  public Task AddOption(ClaimsPrincipal user, Guid poll, string text) => Write(user, async (db, a) =>
  {
    await Poll(db, a, poll);
    if (await db.Set<PollOption>().CountAsync(x => x.PollId == poll) >= 100) throw new ArgumentException("Достигнут лимит вариантов.");
    db.Add(new PollOption { PollId = poll, AuthorId = a.Id, Text = Text(text, 500) }); return true;
  });

  /// <inheritdoc />
  public async Task<int> OptionVotes(ClaimsPrincipal user, Guid option)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    var o = await db.Set<PollOption>().SingleOrDefaultAsync(x => x.Id == option) ?? throw new UnauthorizedAccessException();
    await Poll(db, a, o.PollId);
    if (!a.Admin && o.AuthorId != a.Id) throw new UnauthorizedAccessException();
    return await db.Set<PollVote>().CountAsync(x => x.OptionId == option);
  }

  /// <inheritdoc />
  public Task RemoveOption(ClaimsPrincipal user, Guid option, int confirmedVotes) => Write(user, async (db, a) =>
  {
    var o = await db.Set<PollOption>().SingleOrDefaultAsync(x => x.Id == option) ?? throw new UnauthorizedAccessException();
    await Poll(db, a, o.PollId);
    if (!a.Admin && o.AuthorId != a.Id) throw new UnauthorizedAccessException();
    if (confirmedVotes != await db.Set<PollVote>().CountAsync(x => x.OptionId == option)) throw new ArgumentException("Число голосов изменилось. Подтвердите удаление заново.");
    db.Remove(o); return true;
  });

  /// <inheritdoc />
  public Task Vote(ClaimsPrincipal user, Guid poll, Guid[] options) => Write(user, async (db, a) =>
  {
    var p = await Poll(db, a, poll);
    options = options.Distinct().ToArray();
    var valid = await db.Set<PollOption>().Where(x => x.PollId == poll).Select(x => x.Id).ToListAsync();
    if ((!p.Multiple && options.Length > 1) || options.Any(x => !valid.Contains(x))) throw new ArgumentException("Недопустимый выбор.");
    var old = await db.Set<PollVote>().Where(x => x.UserId == a.Id && valid.Contains(x.OptionId)).ToListAsync();
    db.RemoveRange(old.Where(x => !options.Contains(x.OptionId)));
    foreach (var id in options.Where(x => old.All(v => v.OptionId != x))) db.Add(new PollVote { OptionId = id, UserId = a.Id });
    return true;
  });
}
