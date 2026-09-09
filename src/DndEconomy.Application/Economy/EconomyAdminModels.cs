using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Economy;

public sealed record CitySummary
{
  public required Guid Id { get; init; }
  public required string Name { get; init; }
  public required CitySize Size { get; init; }
}

/// <summary>Одна строка матрицы "Номинал × Город" — доля приёма по каждому городу, где она задана явно.</summary>
public sealed record CoinAcceptanceMatrixRow
{
  public required CoinDenomination Denomination { get; init; }

  /// <summary>Доля приёма по Id города. Отсутствие ключа = 1 (принимается по номиналу без скидки).</summary>
  public required IReadOnlyDictionary<Guid, decimal> AcceptanceRateByCityId { get; init; }
}

/// <summary>
/// Полная матрица приёма номиналов для редактора в админке. В отличие от
/// <see cref="CityModifierMatrix"/>, строки (номиналы) фиксированы и присутствуют всегда —
/// их не создаёт и не удаляет админ, только правит значения ячеек.
/// </summary>
public sealed record CoinAcceptanceMatrix
{
  public required IReadOnlyList<CitySummary> Cities { get; init; }
  public required IReadOnlyList<CoinAcceptanceMatrixRow> Rows { get; init; }

  /// <summary>Заметки о нюансах приёма монет по Id города (см. City.CoinAcceptanceNote).</summary>
  public required IReadOnlyDictionary<Guid, string?> NotesByCityId { get; init; }
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

  /// <summary>
  /// True, если Тип+Подтип этой строки принадлежит хотя бы одному <c>Item.IsService</c>.
  /// Для таких строк коэффициент — не наценка/скидка к цене товара, а доля суммы, которую
  /// вернут игроку после комиссии, и отсутствие ячейки для города означает "услуга здесь не
  /// оказывается" — НЕ коэффициент 1, в отличие от обычных товарных строк (см. AdminCityModifiers.razor).
  /// </summary>
  public required bool IsService { get; init; }
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
