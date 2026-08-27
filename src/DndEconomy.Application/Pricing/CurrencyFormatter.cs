namespace DndEconomy.Application.Pricing;

/// <summary>
/// Форматирует стоимость (хранится и считается всегда в золотых монетах, зм — см. CLAUDE.md)
/// в подходящий номинал классической монетной системы ДнД: платина (пм) → золото (зм) →
/// электрум (эм) → серебро (см) → медь (мм). Выбирается самый крупный номинал, в котором
/// значение получается целым числом монет — дробных монет не бывает физически (нельзя
/// "поделить" серебряную монету пополам), поэтому сумма сперва округляется до целого числа
/// серебряных монет (0.1 зм — минимальный шаг для пм/зм/эм/см), а для сумм дешевле одной
/// серебряной монеты используется медь (мм), округлённая до целой монеты — медная монета сама
/// является минимальной неделимой единицей стоимости, "1.5 мм" физически не существует.
/// </summary>
public static class CurrencyFormatter
{
  /// <summary>Стоимость одной медной монеты в золотых монетах — минимальный шаг всей системы.</summary>
  private const decimal CopperGoldValue = 0.01m;

  private static readonly (string Abbreviation, string Emoji, decimal GoldValue)[] Denominations =
  [
    ("пм", "💎", 10m),
    ("зм", "🟡", 1m),
    ("эм", "🟠", 0.5m),
    ("см", "⚪", 0.1m)
  ];

  private const string CopperAbbreviation = "мм";
  private const string CopperEmoji = "🟤";

  /// <summary>Форматирует сумму в золотых монетах в строку вида "12 🟡 зм".</summary>
  public static string Format(decimal amountInGold)
  {
    if (amountInGold == 0)
      return $"0 {CopperEmoji} {CopperAbbreviation}";

    var sign = amountInGold < 0 ? "-" : "";
    var absAmount = Math.Abs(amountInGold);

    // Дробных монет не бывает: серебро (и всё крупнее) округляется до целого числа серебряных
    // монет (шаг 0.1 зм), медь — до целой монеты в отдельной ветке ниже.
    var roundedToSilver = Math.Round(absAmount, 1, MidpointRounding.AwayFromZero);

    if (roundedToSilver == 0m)
    {
      var copperCoins = Math.Max(1m, Math.Round(absAmount / CopperGoldValue, 0, MidpointRounding.AwayFromZero));
      return $"{sign}{copperCoins.ToString("0")} {CopperEmoji} {CopperAbbreviation}";
    }

    foreach (var (abbreviation, emoji, goldValue) in Denominations)
    {
      var displayValue = roundedToSilver / goldValue;
      if (displayValue >= 1m && displayValue == Math.Floor(displayValue))
        return $"{sign}{displayValue.ToString("0")} {emoji} {abbreviation}";
    }

    // Недостижимо: серебро (последний элемент Denominations) всегда даёт целое число,
    // так как roundedToSilver кратен его номиналу (0.1 зм) по построению.
    return $"{sign}{roundedToSilver.ToString("0.#")} 🟡 зм";
  }
}
