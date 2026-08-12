using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Игровая сессия ("Партия" в исходной таблице) — снимок состояния мира на определённую
/// реальную дату: где находится группа, какой сейчас сезон и какие глобальные коэффициенты
/// действуют. PriceCalculationService всегда берёт сессию с ближайшей RealDateUtc, не позже
/// текущей даты, как это делает XLOOKUP в исходном файле.
/// </summary>
public class EconomySession : AuditableEntity
{
  #region Идентификация сессии

  /// <summary>Человекочитаемое название сессии (например, "Партия 8").</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Свободный комментарий мастера к сессии.</summary>
  public string? Description { get; set; }

  #endregion

  #region Временная привязка

  /// <summary>
  /// Реальная дата, с которой эта сессия становится активной. Используется для поиска
  /// "текущей" сессии — берётся последняя по дате запись, не превышающая сегодня.
  /// </summary>
  public DateOnly RealDate { get; set; }

  /// <summary>
  /// Игровая дата в свободном формате (в исходнике встречаются как обычные даты, так и
  /// произвольные строки вида "26.03.1300" для нестандартного календаря кампании).
  /// </summary>
  public string GameDateLabel { get; set; } = string.Empty;

  #endregion

  #region Состояние мира

  /// <summary>Город, в котором сейчас находится партия — определяет применяемые CityModifier.</summary>
  public Guid? CityId { get; set; }

  /// <summary>Навигационное свойство на текущий город.</summary>
  public City? City { get; set; }

  /// <summary>Текущий игровой сезон — определяет применяемые SeasonModifier.</summary>
  public Season Season { get; set; }

  /// <summary>
  /// Ручное закрепление: администратор форсирует показ цен именно этой сессии игрокам,
  /// в обход обычного выбора "последняя сессия с RealDate не позже сегодня". Как только
  /// у другой сессии наступает RealDate позже, чем у закреплённой, автоматический выбор
  /// снова берёт верх — см. EconomyPricingReadStore.GetActiveSessionContextAsync. Не более
  /// одной сессии одновременно (обеспечивается частичным уникальным индексом в конфигурации).
  /// </summary>
  public bool IsPinnedForDisplay { get; set; }

  #endregion

  #region Глобальные коэффициенты

  /// <summary>
  /// Базовый коэффициент сессии (столбец "Базовый коэффициент" в "Настройках") —
  /// общий множитель цен, например, из-за инфляции или дефицита в регионе.
  /// </summary>
  public decimal BaseCoefficient { get; set; } = 1m;

  /// <summary>
  /// Коэффициент, с которым лавки скупают предметы у игроков (обычно меньше 1, скупка дешевле продажи).
  /// </summary>
  public decimal SellCoefficient { get; set; } = 0.9m;

  #endregion
}
