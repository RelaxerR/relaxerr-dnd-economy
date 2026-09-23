using DndEconomy.Domain.Entities;

namespace DndEconomy.Application.Activities;

/// <summary>
/// Проверка строки коэффициента размера партии — общая для ручного ввода на странице
/// калькулятора и Excel-импорта (см. <see cref="EconomyActivityValidator"/> — тот же приём).
/// </summary>
public static class PartySizeCoefficientValidator
{
  /// <summary>Возвращает текст ошибки или null, если строка корректна.</summary>
  public static string? Validate(int partySize, decimal coefficient)
  {
    if (partySize < PartySizeCoefficient.MinPartySize || partySize > PartySizeCoefficient.MaxPartySize)
      return $"Размер партии должен быть от {PartySizeCoefficient.MinPartySize} до {PartySizeCoefficient.MaxPartySize}.";

    if (coefficient <= 0)
      return "Коэффициент должен быть больше нуля.";

    return null;
  }
}
