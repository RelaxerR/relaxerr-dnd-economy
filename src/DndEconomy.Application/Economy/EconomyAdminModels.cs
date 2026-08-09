using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Economy;

public sealed record CitySummary
{
  public required Guid Id { get; init; }
  public required string Name { get; init; }
  public required CitySize Size { get; init; }
}

public sealed record EconomySessionSummary
{
  public required Guid Id { get; init; }
  public required string Name { get; init; }
  public string? Description { get; init; }
  public required DateOnly RealDate { get; init; }
  public required string GameDateLabel { get; init; }
  public Guid? CityId { get; init; }
  public string? CityName { get; init; }
  public required Season Season { get; init; }
  public required decimal BaseCoefficient { get; init; }
  public required decimal SellCoefficient { get; init; }
}

public sealed record NewEconomySessionInput
{
  public required string Name { get; init; }
  public string? Description { get; init; }
  public required DateOnly RealDate { get; init; }
  public required string GameDateLabel { get; init; }
  public Guid? CityId { get; init; }
  public required Season Season { get; init; }
  public required decimal BaseCoefficient { get; init; }
  public required decimal SellCoefficient { get; init; }
}
