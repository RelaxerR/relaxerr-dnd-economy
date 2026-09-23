using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Activities;

/// <summary>
/// Проверка инвариантов экономической активности — одна и та же для ручного ввода в админке
/// (<see cref="IEconomyActivityAdminService"/>) и Excel-импорта, чтобы некорректная строка
/// давала понятное сообщение, а не исключение от CHECK-ограничения БД.
/// </summary>
public static class EconomyActivityValidator
{
  /// <summary>Минимально допустимый уровень персонажа.</summary>
  public const int MinCharacterLevel = 1;

  /// <summary>Максимально допустимый уровень персонажа.</summary>
  public const int MaxCharacterLevel = 20;

  /// <summary>Возвращает текст первой найденной ошибки или null, если ввод корректен.</summary>
  public static string? Validate(NewEconomyActivityInput input)
  {
    if (string.IsNullOrWhiteSpace(input.Category))
      return "Категория обязательна.";

    if (input.ActivityType == EconomyActivityType.Quest && input.DangerLevel is null)
      return "У задания должен быть указан уровень опасности.";

    if (input.MinLevel < MinCharacterLevel || input.MaxLevel > MaxCharacterLevel || input.MinLevel > input.MaxLevel)
      return $"Уровни должны быть в диапазоне {MinCharacterLevel}..{MaxCharacterLevel}, минимальный — не больше максимального.";

    if (input.RateMin > input.RateMax)
      return "Минимальная ставка не может быть больше максимальной.";

    if (input.RecommendedDurationDays < 1)
      return "Рекомендуемая длительность — не меньше 1 дня.";

    return null;
  }
}
