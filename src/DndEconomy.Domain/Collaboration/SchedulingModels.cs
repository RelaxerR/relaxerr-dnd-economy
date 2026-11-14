namespace DndEconomy.Domain.Collaboration;

/// <summary>Пять состояний календаря, от подтверждённого до незаполненного.</summary>
public enum AvailabilityStatus { Certain, PreferOther, Unlikely, Unavailable, Empty }
/// <summary>Форматы игры, обрабатываемые независимо.</summary>
[Flags]
public enum PlayFormat { Online = 1, Offline = 2, Both = 3 }
/// <summary>Локальное правило повторения календаря.</summary>
public enum RepeatKind { Once, Daily, Weekly, Monthly }

/// <summary>Локальное правило доступности; разовые окна дополнительно хранят абсолютные границы.</summary>
public sealed class AvailabilityRule
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UserId { get; set; }
  public DateTime LocalStart { get; set; }
  public DateTime LocalEnd { get; set; }
  public string Zone { get; set; } = "Europe/Samara";
  public DateTime? StartUtc { get; set; }
  public DateTime? EndUtc { get; set; }
  public AvailabilityStatus Status { get; set; }
  public PlayFormat Format { get; set; } = PlayFormat.Both;
  /// <summary>Последний день серии включительно; null — повтор без даты окончания.</summary>
  public DateOnly? RepeatUntil { get; set; }
  public RepeatKind Repeat { get; set; }
  public int[] Days { get; set; } = [];
  public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Настройки мастера для поиска и оценки вариантов.</summary>
public sealed record SchedulingSettings
{
  public int HorizonDays { get; set; } = 7;
  public int DurationMinutes { get; set; } = 180;
  public int StepMinutes { get; set; } = 30;
  public int MinimumPlayers { get; set; } = 3;
  public PlayFormat Format { get; set; } = PlayFormat.Both;
  public int FourLateMinutes { get; set; } = 60;
  public int FiveLateMinutes { get; set; } = 120;
  public int SixLateMinutes { get; set; } = 120;
  public int SixLateCount { get; set; } = 2;
  public double LatenessWeight { get; set; } = 1;
}

/// <summary>Устойчивый запрос; состав голосующих фиксируется при открытии каждого опроса.</summary>
public sealed class SchedulingRequest
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RoomId { get; set; }
  public Guid CardMessageId { get; set; }
  public Guid MasterId { get; set; }
  public string Zone { get; set; } = "Europe/Samara";
  public DateTime StartUtc { get; set; }
  public DateTime EndUtc { get; set; }
  public string SettingsJson { get; set; } = "{}";
  public string OptionsJson { get; set; } = "[]";
  public Guid[] Voters { get; set; } = [];
  public int Round { get; set; }
  public string State { get; set; } = "waiting";
}

/// <summary>Ответ в конкретном раунде, в том числе пустой выбор.</summary>
public sealed class SchedulingBallot
{
  public Guid RequestId { get; set; }
  public int Round { get; set; }
  public Guid UserId { get; set; }
  public string[] Keys { get; set; } = [];
}

/// <summary>Назначенная игра; отмена сохраняет историю и прежние экономические данные.</summary>
public sealed class ScheduledGame
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RequestId { get; set; }
  public int Round { get; set; }
  public Guid MasterId { get; set; }
  public DateTime StartUtc { get; set; }
  public DateTime EndUtc { get; set; }
  public PlayFormat Format { get; set; }
  public DateTime? CancelledUtc { get; set; }
  public Guid? EconomySessionId { get; set; }
}
