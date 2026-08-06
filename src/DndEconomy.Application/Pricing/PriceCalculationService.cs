using Microsoft.Extensions.Logging;

namespace DndEconomy.Application.Pricing;

/// <summary>
/// Реализация <see cref="IPriceCalculationService"/>. Повторяет формулы исходной Excel-таблицы:
/// РассчитаннаяСтоимость = БазоваяСтоимость × КэфСессии × КэфГорода × КэфСезона;
/// СтоимостьПродажи = РассчитаннаяСтоимость × КэфПродажи (либо штрафной откуп, если товара нет в наличии).
/// </summary>
public sealed class PriceCalculationService : IPriceCalculationService
{
  #region Поля и конструктор

  private readonly IEconomyPricingReadStore _readStore;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<PriceCalculationService> _logger;

  public PriceCalculationService(
    IEconomyPricingReadStore readStore,
    TimeProvider timeProvider,
    ILogger<PriceCalculationService> logger)
  {
    _readStore = readStore;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Публичный метод

  /// <inheritdoc />
  public async Task<ItemPriceResult?> CalculateCurrentPriceAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _readStore.GetItemPricingSourceAsync(itemId, cancellationToken);
    if (item is null)
    {
      _logger.LogWarning("Расчёт цены: предмет {ItemId} не найден", itemId);
      return null;
    }

    var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    var session = await _readStore.GetActiveSessionContextAsync(today, cancellationToken);
    if (session is null)
    {
      _logger.LogWarning("Расчёт цены: нет ни одной активной сессии на дату {Date}", today);
      return null;
    }

    var cityCoefficient = await _readStore.GetCityCoefficientAsync(item.Type, item.Subtype, session.CityId, cancellationToken);
    var seasonCoefficient = await _readStore.GetSeasonCoefficientAsync(item.Type, item.Subtype, session.Season, cancellationToken);

    var calculatedCost = PriceFormulas.CalculateRawCost(item.BaseCost, session.BaseCoefficient, cityCoefficient, seasonCoefficient);
    var buyPrice = PriceFormulas.ResolveBuyPrice(calculatedCost);
    var sellPrice = PriceFormulas.ResolveSellPrice(calculatedCost, item.BaseCost, session.SellCoefficient);

    _logger.LogInformation(
      "Цена рассчитана: предмет {ItemId}, сессия {Session}, город {City}, покупка {BuyPrice}, продажа {SellPrice}",
      itemId, session.SessionName, session.CityName, buyPrice, sellPrice);

    return new ItemPriceResult
    {
      ItemId = itemId,
      BuyPrice = buyPrice,
      SellPrice = sellPrice,
      ActiveSessionName = session.SessionName,
      CityName = session.CityName,
      SeasonName = session.Season.ToString()
    };
  }

  #endregion
}
