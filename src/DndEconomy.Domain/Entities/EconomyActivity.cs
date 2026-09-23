using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Справочная строка экономической активности — ставка дохода (или расхода) в зм за день
/// НА ОДНОГО ИГРОКА для диапазона уровней персонажей. Покрывает и партийные задания
/// (<see cref="EconomyActivityType.Quest"/>), и персональные занятия в простое
/// (<see cref="EconomyActivityType.Downtime"/>). Admin-only — игроки этот справочник не видят
/// нигде: ни в каталоге, ни в расчёте цены предметов. Уникального ключа по координатам нет —
/// несколько строк с одинаковыми Типом+Категорией+Уровнями допустимы (это список, а не
/// матрица коэффициентов).
/// </summary>
public class EconomyActivity : AuditableEntity
{
  /// <summary>Вид активности — партийное задание или занятие в простое.</summary>
  public EconomyActivityType ActivityType { get; set; }

  /// <summary>
  /// Категория — например, "Дипломатия", "Курьерские", "Эскорт" у заданий; "Практика
  /// профессии", "Кутёж", "Азартные игры" у простоя.
  /// </summary>
  public string Category { get; set; } = string.Empty;

  /// <summary>
  /// Уровень опасности — задан только у <see cref="EconomyActivityType.Quest"/>, у
  /// <see cref="EconomyActivityType.Downtime"/> всегда null (инвариант закреплён CHECK-ограничением в БД).
  /// </summary>
  public QuestDangerLevel? DangerLevel { get; set; }

  /// <summary>Минимальный уровень персонажей, для которого актуальна ставка (1..20, включительно).</summary>
  public int MinLevel { get; set; }

  /// <summary>Максимальный уровень персонажей, для которого актуальна ставка (1..20, включительно).</summary>
  public int MaxLevel { get; set; }

  /// <summary>
  /// Нижняя граница ставки в зм за день НА ОДНОГО ИГРОКА — всегда на игрока, без исключений,
  /// и для заданий, и для простоя. Может быть отрицательной: часть занятий в простое — расход
  /// (обучение, кутёж), а не доход.
  /// </summary>
  public decimal RateMin { get; set; }

  /// <summary>
  /// Верхняя граница ставки в зм за день НА ОДНОГО ИГРОКА (см. <see cref="RateMin"/>),
  /// не меньше <see cref="RateMin"/>.
  /// </summary>
  public decimal RateMax { get; set; }

  /// <summary>
  /// Рекомендуемая длительность в днях — одно число, справочный ориентир.
  /// <para>
  /// ВАЖНО: реальная выплата за конкретный контракт — это <c>Rate × RecommendedDurationDays</c>,
  /// посчитанное ОДИН РАЗ как фиксированная сумма. Она НЕ пересчитывается от фактического
  /// количества дней, которое ушло у стола — иначе получается почасовой контракт, где тянуть
  /// время выгоднее, чем работать быстро. Бонусы за скорость и штрафы за срыв сроков мастер
  /// назначает поверх этой суммы вручную.
  /// </para>
  /// </summary>
  public int RecommendedDurationDays { get; set; }

  /// <summary>Описание активности (текст задания или занятия).</summary>
  public string Description { get; set; } = string.Empty;

  /// <summary>Примечание мастера по балансу ставки. Null, если примечания нет.</summary>
  public string? BalanceNote { get; set; }
}
