namespace DndEconomy.Domain.Enums;

/// <summary>
/// Номинал монеты D&amp;D. Соответствует нативным полям валюты в Foundry VTT (cp/sp/ep/gp/pp) —
/// сайт не хранит и не двигает деньги игроков, только показывает справочные условия приёма
/// по городу (см. <see cref="DndEconomy.Domain.Entities.CityCoinAcceptance"/>).
/// </summary>
public enum CoinDenomination
{
  /// <summary>Медная монета (cp).</summary>
  Copper = 0,

  /// <summary>Серебряная монета (sp).</summary>
  Silver = 1,

  /// <summary>Электрумовая монета (ep).</summary>
  Electrum = 2,

  /// <summary>Золотая монета (gp).</summary>
  Gold = 3,

  /// <summary>Платиновая монета (pp).</summary>
  Platinum = 4
}
