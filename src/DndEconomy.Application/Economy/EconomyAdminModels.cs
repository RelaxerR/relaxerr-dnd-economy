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
  public required bool IsPinnedForDisplay { get; init; }
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

/// <summary>Пара Тип+Подтип, как она заведена у предметов каталога — ключ для строки матрицы коэффициентов.</summary>
public sealed record TypeSubtype
{
  public required string Type { get; init; }
  public required string Subtype { get; init; }
}

/// <summary>Одна строка матрицы "Тип+Подтип × Город" — коэффициент по каждому городу, где он задан явно.</summary>
public sealed record CityModifierMatrixRow
{
  public required string Type { get; init; }
  public required string Subtype { get; init; }

  /// <summary>Коэффициент по Id города. Отсутствие ключа = коэффициент 1 (без изменений).</summary>
  public required IReadOnlyDictionary<Guid, decimal> CoefficientsByCityId { get; init; }
}

/// <summary>Полная матрица коэффициентов по городам для редактора в админке.</summary>
public sealed record CityModifierMatrix
{
  public required IReadOnlyList<CitySummary> Cities { get; init; }
  public required IReadOnlyList<CityModifierMatrixRow> Rows { get; init; }
}

/// <summary>Одна строка матрицы "Тип+Подтип × Сезон" — коэффициент по каждому из 4 сезонов, где он задан явно.</summary>
public sealed record SeasonModifierMatrixRow
{
  public required string Type { get; init; }
  public required string Subtype { get; init; }

  /// <summary>Коэффициент по сезону. Отсутствие ключа = коэффициент 1 (без изменений).</summary>
  public required IReadOnlyDictionary<Season, decimal> CoefficientsBySeason { get; init; }
}
