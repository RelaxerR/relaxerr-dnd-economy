using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Activities;

/// <summary>
/// Русские подписи вида активности и уровня опасности — одни и те же на странице админки и
/// в Excel-листе "Экономическая активность", чтобы выгруженный файл читался импортом обратно
/// без расхождений в написании.
/// </summary>
public static class EconomyActivityLabels
{
  /// <summary>Порядок видов активности в интерфейсе.</summary>
  public static readonly EconomyActivityType[] TypeOrder = [EconomyActivityType.Quest, EconomyActivityType.Downtime];

  /// <summary>Порядок уровней опасности в интерфейсе — от тривиальной к смертельной.</summary>
  public static readonly QuestDangerLevel[] DangerOrder =
    [QuestDangerLevel.Trivial, QuestDangerLevel.Easy, QuestDangerLevel.Standard, QuestDangerLevel.Dangerous, QuestDangerLevel.Deadly];

  /// <summary>Подпись вида активности в единственном числе ("Задание"/"Простой").</summary>
  public static string Type(EconomyActivityType type) => type switch
  {
    EconomyActivityType.Quest => "Задание",
    EconomyActivityType.Downtime => "Простой",
    _ => type.ToString()
  };

  /// <summary>Подпись уровня опасности ("Тривиальная".."Смертельная").</summary>
  public static string Danger(QuestDangerLevel level) => level switch
  {
    QuestDangerLevel.Trivial => "Тривиальная",
    QuestDangerLevel.Easy => "Лёгкая",
    QuestDangerLevel.Standard => "Стандартная",
    QuestDangerLevel.Dangerous => "Опасная",
    QuestDangerLevel.Deadly => "Смертельная",
    _ => level.ToString()
  };

  /// <summary>Разбор подписи вида активности (без учёта регистра и пробелов по краям).</summary>
  public static bool TryParseType(string label, out EconomyActivityType type)
    => TryParse(label, TypeOrder, Type, out type);

  /// <summary>Разбор подписи уровня опасности (без учёта регистра и пробелов по краям).</summary>
  public static bool TryParseDanger(string label, out QuestDangerLevel level)
    => TryParse(label, DangerOrder, Danger, out level);

  private static bool TryParse<T>(string label, T[] values, Func<T, string> toLabel, out T result)
  {
    var trimmed = label.Trim();
    foreach (var value in values)
    {
      if (string.Equals(toLabel(value), trimmed, StringComparison.OrdinalIgnoreCase))
      {
        result = value;
        return true;
      }
    }

    result = default!;
    return false;
  }
}
