using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Игровой населённый пункт (соответствует одному столбцу листа "Города"). Хранится отдельной
/// сущностью, а не строкой, чтобы админ мог добавлять новые города через интерфейс без правки кода.
/// </summary>
public class City : AuditableEntity
{
  /// <summary>Название города (например, "Ларилоу").</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Категория населённого пункта — влияет на то, какие типы товаров в нём доступны.</summary>
  public CitySize Size { get; set; }

  #region Навигационные свойства

  /// <summary>Модификаторы стоимости товаров, специфичные для этого города.</summary>
  public ICollection<CityModifier> Modifiers { get; set; } = new List<CityModifier>();

  #endregion
}
