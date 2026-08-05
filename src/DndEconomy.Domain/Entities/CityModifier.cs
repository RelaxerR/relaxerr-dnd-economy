using DndEconomy.Domain.Common;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Коэффициент стоимости для пары "Тип + Подтип" предмета в конкретном городе.
/// Нормализованное представление матрицы с листа "Города" (там строки — Тип/Подтип,
/// столбцы — города, на пересечении — коэффициент).
/// </summary>
public class CityModifier : AuditableEntity
{
  /// <summary>Тип предметов, к которым применяется коэффициент (например, "Оружие").</summary>
  public string Type { get; set; } = string.Empty;

  /// <summary>Подтип предметов (например, "Расширенное огнестрельное").</summary>
  public string Subtype { get; set; } = string.Empty;

  /// <summary>Город, к которому относится коэффициент.</summary>
  public Guid CityId { get; set; }

  /// <summary>Навигационное свойство на город.</summary>
  public City? City { get; set; }

  /// <summary>
  /// Множитель стоимости. 0 означает "товар этой категории в этом городе не продаётся"
  /// (в расчёте цены даст статус "Нет в наличии").
  /// </summary>
  public decimal Coefficient { get; set; } = 1m;
}
