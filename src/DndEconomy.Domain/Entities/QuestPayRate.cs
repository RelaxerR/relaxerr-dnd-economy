using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Справочная строка таблицы оплаты заданий (Категория × Опасность × Эпоха → оплата партии
/// в зм), которой мастер пользуется при подготовке к сессии. Admin-only — обычная роль игрока
/// эту таблицу не видит нигде: ни в каталоге, ни в расчёте цены. В отличие от CityModifier/
/// SeasonModifier здесь нет пересчёта по городу/сезону/сессии и нет уникального ключа по
/// координатам — несколько заданий одной Категории+Опасности+Эпохи допустимы (это не матрица
/// коэффициентов, а список конкретных заданий).
/// </summary>
public class QuestPayRate : AuditableEntity
{
  /// <summary>Эпоха кампании (диапазон уровней партии).</summary>
  public QuestEpoch Epoch { get; set; }

  /// <summary>Категория задания — например, "Дипломатия", "Экспедиции", "Эскорт и охрана".</summary>
  public string Category { get; set; } = string.Empty;

  /// <summary>Уровень опасности задания.</summary>
  public QuestDangerLevel DangerLevel { get; set; }

  /// <summary>Текст задания.</summary>
  public string Description { get; set; } = string.Empty;

  /// <summary>Свободный текст продолжительности задания, например "1-2 дня".</summary>
  public string Duration { get; set; } = string.Empty;

  /// <summary>Оплата в зм на партию целиком.</summary>
  public int PartyPayment { get; set; }

  /// <summary>Примечание мастера по балансу оплаты этого задания.</summary>
  public string BalanceNote { get; set; } = string.Empty;
}
