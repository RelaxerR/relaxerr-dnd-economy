using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Constants;

/// <summary>
/// Справочные данные по номиналам монет: отображаемое имя, аббревиатура и фиксированный курс
/// между номиналами (100мс = 10сс = 2эс = 1зм = 1/10пп — не пересчитывается, курс игры, не
/// изменяется из админки, в отличие от <see cref="DndEconomy.Domain.Entities.CityCoinAcceptance.AcceptanceRate"/>).
/// </summary>
public static class CoinDenominations
{
  /// <summary>Одна строка справочной таблицы курса номиналов.</summary>
  public sealed record Info(CoinDenomination Denomination, string DisplayName, string Abbreviation, int ValueInCopperPieces);

  /// <summary>Все номиналы по возрастанию ценности — порядок для отображения в таблицах.</summary>
  public static readonly IReadOnlyList<Info> All =
  [
    new(CoinDenomination.Copper, "Медь", "мс", 1),
    new(CoinDenomination.Silver, "Серебро", "сс", 10),
    new(CoinDenomination.Electrum, "Электрум", "эс", 50),
    new(CoinDenomination.Gold, "Золото", "зм", 100),
    new(CoinDenomination.Platinum, "Платина", "пп", 1000)
  ];
}
