using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Pricing;

/// <summary>
/// Минимальный набор полей предмета, нужный для расчёта цены (без лишней загрузки всей сущности).
/// </summary>
public sealed class ItemPricingSource
{
  public required Guid ItemId { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required decimal BaseCost { get; init; }
}

/// <summary>
/// Снимок состояния мира из активной на заданную дату сессии ("Партии") — город, сезон,
/// глобальные коэффициенты. Аналог того, что для строки таблицы возвращали формулы
/// O2/P2/Q2/I2/L2 в исходном файле.
/// </summary>
public sealed class ActiveSessionContext
{
  public required string SessionName { get; init; }
  public required Guid CityId { get; init; }
  public required string CityName { get; init; }
  public required Season Season { get; init; }
  public required decimal BaseCoefficient { get; init; }
  public required decimal SellCoefficient { get; init; }
}
