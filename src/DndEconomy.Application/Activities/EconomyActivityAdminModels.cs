using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Activities;

public sealed record EconomyActivitySummary
{
  public required Guid Id { get; init; }
  public required EconomyActivityType ActivityType { get; init; }
  public required string Category { get; init; }
  public required QuestDangerLevel? DangerLevel { get; init; }
  public required int MinLevel { get; init; }
  public required int MaxLevel { get; init; }
  public required decimal RateMin { get; init; }
  public required decimal RateMax { get; init; }
  public required int RecommendedDurationDays { get; init; }
  public required string Description { get; init; }
  public required string? BalanceNote { get; init; }
}

public sealed record NewEconomyActivityInput
{
  public required EconomyActivityType ActivityType { get; init; }
  public required string Category { get; init; }

  /// <summary>Игнорируется (сохраняется null) для <see cref="EconomyActivityType.Downtime"/>.</summary>
  public QuestDangerLevel? DangerLevel { get; init; }

  public required int MinLevel { get; init; }
  public required int MaxLevel { get; init; }
  public required decimal RateMin { get; init; }
  public required decimal RateMax { get; init; }
  public required int RecommendedDurationDays { get; init; }
  public required string Description { get; init; }
  public string? BalanceNote { get; init; }
}
