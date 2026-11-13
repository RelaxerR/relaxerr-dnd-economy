using DndEconomy.Domain.Collaboration;

namespace DndEconomy.Application.Collaboration;

/// <summary>Непересекающийся абсолютный отрезок календаря.</summary>
public sealed record AvailabilitySlice(Guid UserId, DateTime StartUtc, DateTime EndUtc, AvailabilityStatus Status, PlayFormat Format)
{
  /// <summary>Правило, определившее фактический статус отрезка.</summary>
  public Guid RuleId { get; init; }
}
/// <summary>Участник предлагаемого состава с фактическим приходом.</summary>
public sealed record Arrival(Guid UserId, DateTime AtUtc, AvailabilityStatus Status);
/// <summary>Вариант с устойчивым ключом, составом, альтернативами и причиной цвета.</summary>
public sealed record SessionWindow(string Key, DateTime StartUtc, DateTime EndUtc, PlayFormat Format,
  int Color, AvailabilityStatus MasterStatus, List<Arrival> Players, List<Arrival> Alternatives,
  List<Guid> Absent, double Score, string Reason)
{
  public bool Eligible { get; set; } = true;
}

/// <summary>Чистые функции календаря: DST, разворачивание повторов и наложение исключений.</summary>
public static class CalendarEngine
{
  /// <summary>Переводит однозначное локальное время в UTC; неоднозначное и пропущенное время требует исправления.</summary>
  public static DateTime ToUtc(DateTime local, string zone)
  {
    var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
    local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    if (tz.IsInvalidTime(local) || tz.IsAmbiguousTime(local))
      throw new ArgumentException("Время попадает в переход DST. Выберите однозначное время.");
    return TimeZoneInfo.ConvertTimeToUtc(local, tz);
  }

  /// <summary>Разворачивает локальные повторы и делит пересечения по точным границам.</summary>
  public static List<AvailabilitySlice> Expand(IEnumerable<AvailabilityRule> rules, DateTime from, DateTime until)
  {
    var result = new List<AvailabilitySlice>();
    foreach (var user in rules.GroupBy(x => x.UserId))
    foreach (var format in new[] { PlayFormat.Online, PlayFormat.Offline })
    {
      var raw = new List<(AvailabilityRule Rule, DateTime Start, DateTime End)>();
      foreach (var rule in user.Where(x => (x.Format & format) != 0))
      {
        if (rule.Repeat == RepeatKind.Once)
        {
          if (rule.StartUtc < until && rule.EndUtc > from)
            raw.Add((rule, rule.StartUtc!.Value, rule.EndUtc!.Value));
          continue;
        }
        var tz = TimeZoneInfo.FindSystemTimeZoneById(rule.Zone);
        var first = TimeZoneInfo.ConvertTimeFromUtc(from, tz).Date.AddDays(-1);
        var last = TimeZoneInfo.ConvertTimeFromUtc(until, tz).Date;
        for (var date = first; date <= last; date = date.AddDays(1))
        {
          if (date < rule.LocalStart.Date || date > rule.LocalEnd.Date) continue;
          if (rule.Repeat == RepeatKind.Weekly && !rule.Days.Contains((int)date.DayOfWeek)) continue;
          if (rule.Repeat == RepeatKind.Monthly && !rule.Days.Contains(date.Day)) continue;
          var start = date + rule.LocalStart.TimeOfDay;
          var end = date + rule.LocalEnd.TimeOfDay;
          if (end <= start) end = end.AddDays(1);
          // Повтор в пропущенный или неоднозначный DST-час пропускается, а не сдвигается молча.
          if (tz.IsInvalidTime(start) || tz.IsInvalidTime(end) || tz.IsAmbiguousTime(start) || tz.IsAmbiguousTime(end)) continue;
          raw.Add((rule, ToUtc(start, rule.Zone), ToUtc(end, rule.Zone)));
        }
      }
      var points = raw.SelectMany(x => new[] { x.Start, x.End }).Append(from).Append(until)
        .Where(x => x >= from && x <= until).Distinct().Order().ToArray();
      for (var i = 0; i < points.Length - 1; i++)
      {
        var winner = raw.Where(x => x.Start <= points[i] && x.End >= points[i + 1])
          .OrderBy(x => x.Rule.Repeat == RepeatKind.Once ? 0 : 1)
          .ThenByDescending(x => x.Rule.UpdatedUtc).ThenBy(x => x.Rule.Id).FirstOrDefault();
        if (winner.Rule is not null)
          result.Add(new(user.Key, points[i], points[i + 1], winner.Rule.Status, format) { RuleId = winner.Rule.Id });
      }
    }
    return result;
  }
}

/// <summary>Подбор состава по непрерывному хвосту интервала и лимитам фактического размера партии.</summary>
public static class SessionPlanner
{
  /// <summary>Проверяет настройки, чтобы ограничить стоимость поиска и исключить бессмысленные интервалы.</summary>
  public static void Validate(SchedulingSettings s)
  {
    if (s.HorizonDays is < 7 or > 60 || s.DurationMinutes is < 30 or > 1440 || s.StepMinutes is < 15 or > 240 ||
        s.MinimumPlayers is < 1 or > 20 || s.Format is < PlayFormat.Online or > PlayFormat.Both ||
        s.FourLateMinutes is < 0 or > 1440 || s.FiveLateMinutes is < 0 or > 1440 ||
        s.SixLateMinutes is < 0 or > 1440 || s.SixLateCount is < 0 or > 20 ||
        !double.IsFinite(s.LatenessWeight) || s.LatenessWeight is < 0 or > 100)
      throw new ArgumentException("Недопустимые настройки подбора.");
  }

  /// <summary>Строит и ранжирует окна на абсолютной сетке с начала периода мастера.</summary>
  public static List<SessionWindow> Find(Guid master, IReadOnlyList<Guid> players, List<AvailabilitySlice> calendar,
    DateTime from, DateTime until, SchedulingSettings settings)
  {
    Validate(settings);
    var windows = new List<SessionWindow>();
    for (var start = from; start.AddMinutes(settings.DurationMinutes) <= until; start = start.AddMinutes(settings.StepMinutes))
    foreach (var format in new[] { PlayFormat.Online, PlayFormat.Offline }.Where(x => (settings.Format & x) != 0))
    {
      var window = Evaluate(master, players, calendar, start, start.AddMinutes(settings.DurationMinutes), format, settings);
      if (window is not null) windows.Add(window);
    }
    return windows.OrderBy(x => x.Color).ThenBy(x => x.MasterStatus)
      .ThenByDescending(x => x.Players.Count(p => p.Status < AvailabilityStatus.Unlikely))
      .ThenByDescending(x => x.Score).ThenBy(x => x.StartUtc).ThenBy(x => x.Format).ToList();
  }

  /// <summary>Повторная проверка конкретного стабильного варианта перед назначением.</summary>
  public static SessionWindow? Evaluate(Guid master, IReadOnlyList<Guid> players, List<AvailabilitySlice> calendar,
    DateTime start, DateTime end, PlayFormat format, SchedulingSettings settings)
  {
    Arrival? Tail(Guid id)
    {
      var cursor = end;
      var worst = AvailabilityStatus.Certain;
      foreach (var part in calendar.Where(x => x.UserId == id && x.Format == format && x.StartUtc < end && x.EndUtc > start)
                 .OrderByDescending(x => x.EndUtc))
      {
        if (part.EndUtc < cursor || part.Status >= AvailabilityStatus.Unavailable) break;
        worst = (AvailabilityStatus)Math.Max((int)worst, (int)part.Status);
        cursor = part.StartUtc > start ? part.StartUtc : start;
        if (cursor == start) break;
      }
      return cursor == end ? null : new(id, cursor, worst);
    }
    var gm = Tail(master);
    var available = players.Where(x => x != master).Distinct().Select(Tail).OfType<Arrival>().ToList();
    SessionWindow Red(string reason) => new($"{start.Ticks}:{end.Ticks}:{(int)format}", start, end, format, 3,
      gm?.Status ?? AvailabilityStatus.Empty, [], available, players.Where(x => available.All(p => p.UserId != x)).ToList(), 0, reason) { Eligible = false };
    if (gm is null || gm.AtUtc != start) return Red("Мастер недоступен на весь интервал");
    List<Arrival>? best = null;
    var bestColor = 4;
    var bestScore = double.NegativeInfinity;
    // Для каждого размера и числа опоздавших выбираем лучшие независимые вклады; экспоненциального перебора нет.
    foreach (var conditional in new[] { false, true })
    {
      var pool = available.Where(x => conditional || x.Status < AvailabilityStatus.Unlikely).ToList();
      for (var n = settings.MinimumPlayers; n <= pool.Count; n++)
      {
        var total = n + 1;
        var maxLate = total >= 6 ? settings.SixLateCount : total >= 4 ? 1 : 0;
        var maxMinutes = total >= 6 ? settings.SixLateMinutes : total == 5 ? settings.FiveLateMinutes : settings.FourLateMinutes;
        double Contribution(Arrival p) => (p.Status == AvailabilityStatus.Certain ? 1 : 0) - (p.AtUtc - start).TotalHours * settings.LatenessWeight;
        var onTime = pool.Where(x => x.AtUtc == start).OrderBy(x => x.Status == AvailabilityStatus.Unlikely).ThenByDescending(Contribution).ThenBy(x => x.UserId).ToList();
        var late = pool.Where(x => x.AtUtc > start && (x.AtUtc - start).TotalMinutes <= maxMinutes)
          .OrderBy(x => x.Status == AvailabilityStatus.Unlikely).ThenByDescending(Contribution).ThenBy(x => x.UserId).ToList();
        for (var count = 0; count <= Math.Min(maxLate, n); count++)
        {
          if (onTime.Count < n - count || late.Count < count) continue;
          var chosen = onTime.Take(n - count).Concat(late.Take(count)).ToList();
          var color = gm.Status == AvailabilityStatus.Unlikely || chosen.Any(x => x.Status == AvailabilityStatus.Unlikely) ? 2 :
            gm.Status == AvailabilityStatus.Certain && chosen.All(x => x.Status == AvailabilityStatus.Certain && x.AtUtc == start) ? 0 : 1;
          var score = chosen.Sum(Contribution);
          if (best is null || color < 2 && bestColor == 2 || (color < 2) == (bestColor < 2) && (n > best.Count || n == best.Count && (color < bestColor || color == bestColor && score > bestScore)))
          { best = chosen; bestColor = color; bestScore = score; }
        }
      }
      if (best is not null && bestColor < 2) break;
    }
    if (best is null) return Red("Допустимый состав не набирается");
    var ids = best.Select(x => x.UserId).ToHashSet();
    return new($"{start.Ticks}:{end.Ticks}:{(int)format}", start, end, format, bestColor, gm.Status, best,
      available.Where(x => !ids.Contains(x.UserId)).ToList(), players.Where(x => !ids.Contains(x) && available.All(p => p.UserId != x)).ToList(), bestScore,
      bestColor switch { 0 => "Все точно могут с начала", 1 => "Нежелательное время или допустимое опоздание", _ => "Состав требует подтверждения календарём" });
  }
}
