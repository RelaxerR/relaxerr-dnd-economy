namespace DndEconomy.Application.Pricing;

/// <summary>
/// Форматирует долю после комиссии услуги (например, "Обмен валют") в понятную игроку строку.
/// Используется везде, где может отобразиться Item.IsService: каталог, карточка товара,
/// избранное в профиле — единая формулировка вместо дублирования по каждой странице.
/// </summary>
public static class CommissionFormatter
{
  /// <summary>
  /// <paramref name="commissionRate"/> — доля суммы, которую вернут игроку после комиссии
  /// (1 = без комиссии, 0.93 = комиссия 7%). Null означает, что услуга в этом городе не
  /// оказывается (нет явно заведённого коэффициента CityModifier).
  /// </summary>
  public static string Label(decimal? commissionRate) => commissionRate switch
  {
    null or <= 0 => "Не оказывается в этом городе",
    >= 1 => "Без комиссии",
    _ => $"Комиссия: {(1 - commissionRate.Value):P0}"
  };
}
