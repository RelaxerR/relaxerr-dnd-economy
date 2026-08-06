namespace DndEconomy.Application.Pricing;

/// <summary>
/// Формулы расчёта цены из исходной Excel-модели, общие для одиночного расчёта
/// (<see cref="PriceCalculationService"/>) и bulk-расчёта в каталоге.
/// </summary>
internal static class PriceFormulas
{
  /// <summary>РассчитаннаяСтоимость = БазоваяСтоимость × КэфСессии × КэфГорода × КэфСезона.</summary>
  public static decimal CalculateRawCost(decimal baseCost, decimal sessionCoefficient, decimal cityCoefficient, decimal seasonCoefficient)
    => baseCost * sessionCoefficient * cityCoefficient * seasonCoefficient;

  /// <summary>Цена покупки — null ("Нет в наличии"), если рассчитанная стоимость не положительна.</summary>
  public static decimal? ResolveBuyPrice(decimal calculatedCost)
    => calculatedCost <= 0 ? null : calculatedCost;

  /// <summary>
  /// Цена продажи. Если товара нет в наличии, лавка всё равно готова откупить его дороже
  /// базовой цены (штрафной откуп БазоваяСтоимость×(1+(1-КэфПродажи))), иначе — обычная скидка
  /// от рассчитанной стоимости.
  /// </summary>
  public static decimal ResolveSellPrice(decimal calculatedCost, decimal baseCost, decimal sellCoefficient)
  {
    var candidateSellPrice = calculatedCost * sellCoefficient;
    return candidateSellPrice <= 0
      ? baseCost * (1 + (1 - sellCoefficient))
      : candidateSellPrice;
  }
}
