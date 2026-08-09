namespace DndEconomy.Application.Pricing;

/// <summary>
/// Форматирует стоимость (хранится и считается всегда в золотых монетах, зм — см. CLAUDE.md)
/// в подходящий номинал классической монетной системы ДнД: платина (пм) → золото (зм) →
/// электрум (эм) → серебро (см) → медь (мм). Выбирается самый крупный номинал, в котором
/// значение не меньше единицы — так "1500 зм" читается как "150 пм", а "0.05 зм" как "5 мм".
/// </summary>
public static class CurrencyFormatter
{
  private static readonly (string Abbreviation, decimal GoldValue)[] Denominations =
  [
    ("пм", 10m),
    ("зм", 1m),
    ("эм", 0.5m),
    ("см", 0.1m),
    ("мм", 0.01m)
  ];

  /// <summary>Форматирует сумму в золотых монетах в строку вида "12.5 зм".</summary>
  public static string Format(decimal amountInGold)
  {
    if (amountInGold == 0)
      return "0 мм";

    var sign = amountInGold < 0 ? "-" : "";
    var absAmount = Math.Abs(amountInGold);

    for (var i = 0; i < Denominations.Length; i++)
    {
      var (abbreviation, goldValue) = Denominations[i];
      var isLast = i == Denominations.Length - 1;
      var displayValue = absAmount / goldValue;

      if (displayValue >= 1m || isLast)
        return $"{sign}{displayValue.ToString("0.##")} {abbreviation}";
    }

    return $"{sign}{absAmount.ToString("0.##")} зм";
  }
}
