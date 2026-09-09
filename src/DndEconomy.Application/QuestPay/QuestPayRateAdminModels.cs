using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.QuestPay;

public sealed record QuestPayRateSummary
{
  public required Guid Id { get; init; }
  public required QuestEpoch Epoch { get; init; }
  public required string Category { get; init; }
  public required QuestDangerLevel DangerLevel { get; init; }
  public required string Description { get; init; }
  public required string Duration { get; init; }
  public required int PartyPayment { get; init; }
  public required string BalanceNote { get; init; }
}

public sealed record NewQuestPayRateInput
{
  public required QuestEpoch Epoch { get; init; }
  public required string Category { get; init; }
  public required QuestDangerLevel DangerLevel { get; init; }
  public required string Description { get; init; }
  public required string Duration { get; init; }
  public required int PartyPayment { get; init; }
  public string? BalanceNote { get; init; }
}
