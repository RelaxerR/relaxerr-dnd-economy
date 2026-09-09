using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Условия приёма номинала монеты в конкретном городе — чисто справочная информация для
/// игроков (сайт не хранит и не двигает чьи-либо деньги, кошелёк живёт в Foundry VTT).
/// Нормализованное представление матрицы "Номинал × Город", по тому же паттерну, что и
/// <see cref="CityModifier"/>: отсутствие строки для пары (Город, Номинал) означает
/// "принимается по номиналу без скидки" (<see cref="AcceptanceRate"/> = 1) — админ заводит
/// явно только исключения.
/// </summary>
public class CityCoinAcceptance : AuditableEntity
{
  /// <summary>Номинал монеты, к которому относится условие приёма.</summary>
  public CoinDenomination Denomination { get; set; }

  /// <summary>Город, к которому относится условие приёма.</summary>
  public Guid CityId { get; set; }

  /// <summary>Навигационное свойство на город.</summary>
  public City? City { get; set; }

  /// <summary>
  /// Доля номинала, которую город принимает: 1 — без скидки, 0 &lt; x &lt; 1 — со скидкой
  /// (1-x), 0 — отказывает принимать этот номинал вовсе.
  /// </summary>
  public decimal AcceptanceRate { get; set; } = 1m;
}
