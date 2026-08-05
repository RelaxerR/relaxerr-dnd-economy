using DndEconomy.Domain.Common;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Предмет каталога экономики (оружие, доспехи, инструменты и т.д.). Соответствует одной
/// строке листа "Исходник ДнД" исходной таблицы: хранит базовую (неизменную) стоимость и вес,
/// а итоговая цена с учётом города/сезона/сессии считается на лету в PriceCalculationService.
/// </summary>
public class Item : AuditableEntity
{
  #region Классификация

  /// <summary>Источник/книга правил, откуда взят предмет (например, "Player's Handbook (2024)").</summary>
  public string Category { get; set; } = string.Empty;

  /// <summary>Верхнеуровневый тип предмета (например, "Оружие", "Доспехи").</summary>
  public string Type { get; set; } = string.Empty;

  /// <summary>Подтип предмета (например, "Рукопашное", "Лёгкие"). Используется как ключ для модификаторов города/сезона.</summary>
  public string Subtype { get; set; } = string.Empty;

  #endregion

  #region Название

  /// <summary>Русское название предмета, как оно показывается в поиске и карточке.</summary>
  public string NameRu { get; set; } = string.Empty;

  /// <summary>
  /// Английское название в квадратных скобках из исходника (например, "Automatic Rifle").
  /// Хранится отдельно, чтобы поиск одинаково хорошо находил и "винтовку", и "rifle".
  /// </summary>
  public string? NameEn { get; set; }

  #endregion

  #region Экономика

  /// <summary>Базовая стоимость предмета до применения коэффициентов (столбец H исходной таблицы).</summary>
  public decimal BaseCost { get; set; }

  /// <summary>Вес предмета в фунтах.</summary>
  public decimal Weight { get; set; }

  #endregion

  #region Происхождение

  /// <summary>UUID предмета из справочника Foundry VTT — используется для будущей синхронизации инвентарей.</summary>
  public string? ExternalUuid { get; set; }

  /// <summary>
  /// Признак того, что предмет создан из одобренной заявки игрока, а не при импорте исходной таблицы.
  /// Полезно для аналитики и для отображения бейджа "добавлено сообществом".
  /// </summary>
  public bool IsPlayerSuggested { get; set; }

  #endregion

  #region Навигационные свойства

  /// <summary>Записи "сохранено у игрока", ссылающиеся на этот предмет.</summary>
  public ICollection<UserSavedItem> SavedByUsers { get; set; } = new List<UserSavedItem>();

  #endregion
}
