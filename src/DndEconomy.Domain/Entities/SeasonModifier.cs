using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Коэффициент стоимости для пары "Тип + Подтип" предмета в конкретном сезоне.
/// Нормализованное представление матрицы с листа "Сезонность".
/// </summary>
public class SeasonModifier : AuditableEntity
{
  /// <summary>Тип предметов (например, "Оружие").</summary>
  public string Type { get; set; } = string.Empty;

  /// <summary>Подтип предметов (например, "Расширенное огнестрельное").</summary>
  public string Subtype { get; set; } = string.Empty;

  /// <summary>Сезон, к которому относится коэффициент.</summary>
  public Season Season { get; set; }

  /// <summary>Множитель стоимости в этом сезоне.</summary>
  public decimal Coefficient { get; set; } = 1m;
}
