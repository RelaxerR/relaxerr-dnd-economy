using System.Security.Claims;
using System.Text.Json;
using DndEconomy.Application.Collaboration;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Календарь и автомат согласования; все переходы вместе с сообщениями коммитятся атомарно.</summary>
public sealed class SchedulingService(IDbContextFactory<ApplicationDbContext> factory, TimeProvider clock) : ISchedulingService
{
  private DateTime Now => clock.GetUtcNow().UtcDateTime;

  private async Task<T> Write<T>(ClaimsPrincipal user, Func<ApplicationDbContext, ChatActor, Task<T>> action)
  {
    await using var db = await factory.CreateDbContextAsync();
    await using var tx = await db.Database.BeginTransactionAsync();
    await ChatAccess.Lock(db);
    var a = await ChatAccess.Authenticate(db, user);
    var result = await action(db, a);
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return result;
  }

  /// <inheritdoc />
  public async Task<List<AvailabilityRule>> Calendar(ClaimsPrincipal user)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    return await db.Set<AvailabilityRule>().AsNoTracking().Where(x => x.UserId == a.Id).OrderBy(x => x.LocalStart).ToListAsync();
  }

  /// <inheritdoc />
  public Task SaveRule(ClaimsPrincipal user, AvailabilityRule rule) => Write(user, async (db, a) =>
  {
    rule.LocalStart = DateTime.SpecifyKind(rule.LocalStart, DateTimeKind.Unspecified);
    rule.LocalEnd = DateTime.SpecifyKind(rule.LocalEnd, DateTimeKind.Unspecified);
    _ = TimeZoneInfo.FindSystemTimeZoneById(rule.Zone);
    if (rule.LocalEnd <= rule.LocalStart || rule.LocalEnd > rule.LocalStart.AddYears(2) ||
        (rule.Repeat != RepeatKind.Once && rule.RepeatUntil is { } repeatUntil &&
         (repeatUntil < DateOnly.FromDateTime(rule.LocalStart) || repeatUntil > DateOnly.FromDateTime(rule.LocalStart.AddYears(2)))) ||
        !Enum.IsDefined(rule.Status) || rule.Status == AvailabilityStatus.Empty || !Enum.IsDefined(rule.Repeat) || rule.Format is < PlayFormat.Online or > PlayFormat.Both)
      throw new ArgumentException("Проверьте границы, статус и формат окна.");
    if (rule.Repeat == RepeatKind.Weekly && (rule.Days.Length == 0 || rule.Days.Any(x => x is < 0 or > 6)) ||
        rule.Repeat == RepeatKind.Monthly && (rule.Days.Length == 0 || rule.Days.Any(x => x is < 1 or > 31))) throw new ArgumentException("Укажите дни повтора.");
    var existing = await db.Set<AvailabilityRule>().SingleOrDefaultAsync(x => x.Id == rule.Id);
    if (existing is not null && existing.UserId != a.Id) throw new UnauthorizedAccessException();
    var copy = existing ?? new AvailabilityRule { Id = rule.Id, UserId = a.Id };
    if (existing is null) db.Add(copy);
    copy.LocalStart = rule.LocalStart; copy.LocalEnd = rule.LocalEnd; copy.Zone = rule.Zone;
    copy.RepeatUntil = rule.Repeat == RepeatKind.Once ? null : rule.RepeatUntil;
    copy.Format = rule.Format; copy.Status = rule.Status; copy.Repeat = rule.Repeat; copy.Days = rule.Days.Distinct().ToArray(); copy.UpdatedUtc = Now;
    var lastEdit = await db.Set<AvailabilityRule>().MaxAsync(x => (DateTime?)x.UpdatedUtc);
    if (lastEdit >= copy.UpdatedUtc) copy.UpdatedUtc = lastEdit.Value.AddTicks(10); // PostgreSQL: точность микросекунды.
    copy.StartUtc = rule.Repeat == RepeatKind.Once ? CalendarEngine.ToUtc(rule.LocalStart, rule.Zone) : null;
    copy.EndUtc = rule.Repeat == RepeatKind.Once ? CalendarEngine.ToUtc(rule.LocalEnd, rule.Zone) : null;
    await db.SaveChangesAsync();
    await Recalculate(db, Now); return true;
  });

  /// <inheritdoc />
  public Task DeleteRule(ClaimsPrincipal user, Guid rule) => Write(user, async (db, a) =>
  {
    var r = await db.Set<AvailabilityRule>().SingleOrDefaultAsync(x => x.Id == rule && x.UserId == a.Id) ?? throw new UnauthorizedAccessException();
    db.Remove(r); await db.SaveChangesAsync(); await Recalculate(db, Now); return true;
  });

  private (DateTime Start, DateTime End) Period(string zone, SchedulingSettings s)
  {
    SessionPlanner.Validate(s);
    var tomorrow = TimeZoneInfo.ConvertTimeFromUtc(Now, TimeZoneInfo.FindSystemTimeZoneById(zone)).Date.AddDays(1);
    return (CalendarEngine.ToUtc(tomorrow, zone), CalendarEngine.ToUtc(tomorrow.AddDays(s.HorizonDays), zone));
  }

  internal static async Task<Guid[]> Players(ApplicationDbContext db, Guid master)
  {
    return await db.Users.Where(x => x.Id != master && x.IsActive && !x.MustChangePassword && (x.LockoutEnd == null || x.LockoutEnd <= DateTimeOffset.UtcNow))
      .Where(x => db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Player")))
      .Select(x => x.Id).ToArrayAsync();
  }

  /// <inheritdoc />
  public async Task<List<SessionWindow>> Preview(ClaimsPrincipal user, string zone, SchedulingSettings settings)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    if (!a.Admin) throw new UnauthorizedAccessException();
    var period = Period(zone, settings);
    var players = await Players(db, a.Id);
    var slices = CalendarEngine.Expand(await db.Set<AvailabilityRule>().AsNoTracking().ToListAsync(), period.Start, period.End);
    return SessionPlanner.Find(a.Id, players, slices, period.Start, period.End, settings);
  }

  /// <inheritdoc />
  public Task<Guid> Request(ClaimsPrincipal user, Guid room, string zone, SchedulingSettings settings) => Write(user, async (db, a) =>
  {
    if (!a.Admin) throw new UnauthorizedAccessException();
    var target = await ChatAccess.Room(db, a, room);
    if (target.IsThread || target.Readers.Length > 0) throw new ArgumentException("Запрос создаётся в общем чате кампании.");
    if (await db.Set<SchedulingRequest>().AnyAsync(x => x.MasterId == a.Id && x.State != "completed")) throw new ArgumentException("У мастера уже есть активный запрос.");
    var period = Period(zone, settings);
    var card = new ChatMessage { RoomId = room, AuthorId = a.Id, Text = "Проверьте свои календари", CreatedUtc = Now };
    var request = new SchedulingRequest { RoomId = room, CardMessageId = card.Id, MasterId = a.Id, Zone = zone,
      StartUtc = period.Start, EndUtc = period.End, SettingsJson = JsonSerializer.Serialize(settings) };
    db.Add(card); db.Add(request); target.ActivityUtc = Now;
    await CollaborationNotices.Add(db, card, target, "request", $"request:{request.Id}");
    await db.SaveChangesAsync(); await Recalculate(db, Now); return request.Id;
  });

  /// <inheritdoc />
  public Task Ballot(ClaimsPrincipal user, Guid request, int round, string[] keys) => Write(user, async (db, a) =>
  {
    var r = await db.Set<SchedulingRequest>().SingleOrDefaultAsync(x => x.Id == request) ?? throw new UnauthorizedAccessException();
    await ChatAccess.Room(db, a, r.RoomId);
    if (!r.Voters.Contains(a.Id) || r.State != "voting" || r.Round != round) throw new ArgumentException("Этот раунд голосования завершён или недоступен.");
    var options = JsonSerializer.Deserialize<List<SessionWindow>>(r.OptionsJson)!;
    keys = keys.Distinct().ToArray();
    if (keys.Any(k => options.All(x => x.Key != k || !x.Eligible))) throw new ArgumentException("Вариант больше недоступен.");
    var old = await db.Set<SchedulingBallot>().FindAsync(request, round, a.Id);
    if (old is null) db.Add(new SchedulingBallot { RequestId = request, Round = round, UserId = a.Id, Keys = keys });
    else old.Keys = keys;
    await db.SaveChangesAsync(); await Recalculate(db, Now); return true;
  });

  /// <inheritdoc />
  public Task<Guid> CreateEconomy(ClaimsPrincipal user, Guid game, DndEconomy.Application.Economy.NewEconomySessionInput input) => Write(user, async (db, a) =>
  {
    var g = await db.Set<ScheduledGame>().SingleOrDefaultAsync(x => x.Id == game) ?? throw new UnauthorizedAccessException();
    var request = await db.Set<SchedulingRequest>().SingleAsync(x => x.Id == g.RequestId);
    await ChatAccess.Room(db, a, request.RoomId);
    if (!a.Admin || g.MasterId != a.Id) throw new UnauthorizedAccessException();
    if (g.CancelledUtc != null) throw new ArgumentException("Игра отменена.");
    if (g.EconomySessionId is { } existing) return existing;
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150 || string.IsNullOrWhiteSpace(input.GameDateLabel) || input.GameDateLabel.Length > 100 || input.BaseCoefficient < 0 || input.SellCoefficient < 0)
      throw new ArgumentException("Проверьте поля экономической сессии.");
    var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(g.StartUtc, TimeZoneInfo.FindSystemTimeZoneById(request.Zone)));
    var entity = Economy.EconomyAdminService.NewSession(input with { RealDate = date });
    db.Add(entity); g.EconomySessionId = entity.Id; return entity.Id;
  });

  /// <inheritdoc />
  public async Task<(ScheduledGame Game, DateOnly Date)> Game(ClaimsPrincipal user, Guid game)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    var g = await db.Set<ScheduledGame>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == game) ?? throw new UnauthorizedAccessException();
    var r = await db.Set<SchedulingRequest>().SingleAsync(x => x.Id == g.RequestId);
    await ChatAccess.Room(db, a, r.RoomId);
    if (!a.Admin || g.MasterId != a.Id) throw new UnauthorizedAccessException();
    return (g, DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(g.StartUtc, TimeZoneInfo.FindSystemTimeZoneById(r.Zone))));
  }

  /// <summary>Пересчитывает процессы после календаря и из фонового обработчика под общей блокировкой.</summary>
  public static async Task Recalculate(ApplicationDbContext db, DateTime now)
  {
    var requests = await db.Set<SchedulingRequest>().Where(x => x.State != "completed").ToListAsync();
    var rules = await db.Set<AvailabilityRule>().AsNoTracking().ToListAsync();
    foreach (var r in requests)
    {
      var settings = JsonSerializer.Deserialize<SchedulingSettings>(r.SettingsJson)!;
      var players = await Players(db, r.MasterId);
      var masterActive = await db.Users.AnyAsync(x => x.Id == r.MasterId && x.IsActive && !x.MustChangePassword && (x.LockoutEnd == null || x.LockoutEnd <= DateTimeOffset.UtcNow)) &&
        await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == r.MasterId && role.Name == "Admin" select ur).AnyAsync();
      // Отключённые участники не удерживают раунд и не влияют голосами на результат.
      var activeVoters = players.Concat(masterActive ? [r.MasterId] : Array.Empty<Guid>()).ToHashSet();
      r.Voters = r.Voters.Where(activeVoters.Contains).ToArray();
      var requestRules = rules.Where(x => x.UserId != r.MasterId || masterActive).ToList();
      var room = await db.Set<ChatRoom>().SingleAsync(x => x.Id == r.RoomId);
      var card = await db.Set<ChatMessage>().SingleAsync(x => x.Id == r.CardMessageId);
      var game = await db.Set<ScheduledGame>().SingleOrDefaultAsync(x => x.RequestId == r.Id && x.CancelledUtc == null);
      if (game is not null)
      {
        if (game.EndUtc <= now) { r.State = "completed"; continue; }
        var current = CalendarEngine.Expand(requestRules, game.StartUtc, game.EndUtc);
        var check = SessionPlanner.Evaluate(r.MasterId, players, current, game.StartUtc, game.EndUtc, game.Format, settings);
        if (check is not null && check.Color < 2) continue;
        game.CancelledUtc = now;
        if (game.EconomySessionId is { } economyId)
        {
          var economy = await db.EconomySessions.SingleAsync(x => x.Id == economyId);
          economy.IsCancelled = true; economy.IsPinnedForDisplay = false;
        }
        await Announce(db, r, room, "Игра отменена. Начат новый подбор с завтра.", "cancelled", $"cancelled:{game.Id}", now);
        var tomorrow = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(r.Zone)).Date.AddDays(1);
        r.StartUtc = CalendarEngine.ToUtc(tomorrow, r.Zone); r.EndUtc = CalendarEngine.ToUtc(tomorrow.AddDays(settings.HorizonDays), r.Zone);
        r.State = "waiting"; r.OptionsJson = "[]";
      }
      if (r.EndUtc <= now) { r.State = "completed"; card.Text = "Период запроса завершён."; continue; }
      var calendar = CalendarEngine.Expand(requestRules, r.StartUtc, r.EndUtc);
      var options = JsonSerializer.Deserialize<List<SessionWindow>>(r.OptionsJson)!;
      if (r.State == "voting" || r.State == "no-votes")
      {
        for (var index = 0; index < options.Count; index++)
        {
          var o = options[index];
          var check = SessionPlanner.Evaluate(r.MasterId, players, calendar, o.StartUtc, o.EndUtc, o.Format, settings);
          o.Eligible = o.StartUtc > now && check is not null && check.Color < 2;
          if (o.Eligible) options[index] = check!;
        }
        if (options.All(x => !x.Eligible)) { r.State = "waiting"; options.Clear(); }
      }
      if (r.State == "waiting")
      {
        options = SessionPlanner.Find(r.MasterId, players, calendar, r.StartUtc, r.EndUtc, settings)
          .Where(x => x.Color < 2 && x.StartUtc > now).ToList();
        if (options.Count > 0)
        {
          r.Round++; r.State = "voting"; r.Voters = players.Append(r.MasterId).ToArray();
          await CollaborationNotices.Add(db, card, room, "poll", $"schedule-poll:{r.Id}:{r.Round}");
        }
      }
      r.OptionsJson = JsonSerializer.Serialize(options);
      if (r.State == "voting")
      {
        var ballots = await db.Set<SchedulingBallot>().Where(x => x.RequestId == r.Id && x.Round == r.Round && r.Voters.Contains(x.UserId)).ToListAsync();
        if (r.Voters.All(v => ballots.Any(x => x.UserId == v)))
        {
          var ranked = options.Where(x => x.Eligible).Select(x => new { Window = x, Votes = ballots.Count(b => b.Keys.Contains(x.Key)) }).ToList();
          var max = ranked.Count == 0 ? 0 : ranked.Max(x => x.Votes);
          if (max == 0) { r.State = "no-votes"; card.Text = "Ни одно допустимое окно не получило голосов. Дата не назначена."; }
          else
          {
            var masterKeys = ballots.Single(x => x.UserId == r.MasterId).Keys;
            var winner = ranked.Where(x => x.Votes == max).OrderByDescending(x => masterKeys.Contains(x.Window.Key))
              .ThenBy(x => x.Window.StartUtc).ThenBy(x => x.Window.Format).First().Window;
            var assigned = new ScheduledGame { RequestId = r.Id, Round = r.Round, MasterId = r.MasterId,
              StartUtc = winner.StartUtc, EndUtc = winner.EndUtc, Format = winner.Format };
            db.Add(assigned); r.State = "assigned";
            await Announce(db, r, room, $"Игра назначена: {winner.StartUtc:O} — {winner.EndUtc:O}, {winner.Format}. Если планы изменятся, обновите свои окна в календаре.", "assigned", $"assigned:{assigned.Id}", now);
            var privateCard = new ChatMessage { RoomId = room.Id, AuthorId = r.MasterId, Readers = [r.MasterId],
              Text = $"Настроить партию: /admin/economy/sessions?scheduledGame={assigned.Id}", CreatedUtc = now };
            db.Add(privateCard);
            await CollaborationNotices.Add(db, privateCard, room, "assigned", $"setup:{assigned.Id}");
          }
        }
      }
      if (r.State != "no-votes") card.Text = r.State switch
      {
        "waiting" => "Проверьте свои календари. Допустимых окон пока нет.",
        "voting" => $"Подбор: голосование №{r.Round}. Отметьте все подходящие окна или «Ни один вариант».",
        "assigned" => "Игра назначена. Если планы изменятся, обновите свои окна в календаре.", _ => card.Text
      };
    }
  }

  private static async Task Announce(ApplicationDbContext db, SchedulingRequest request, ChatRoom room, string text, string kind, string key, DateTime now)
  {
    var m = new ChatMessage { RoomId = room.Id, AuthorId = request.MasterId, Text = text, CreatedUtc = now };
    db.Add(m); room.ActivityUtc = now;
    await CollaborationNotices.Add(db, m, room, kind, key);
  }
}
