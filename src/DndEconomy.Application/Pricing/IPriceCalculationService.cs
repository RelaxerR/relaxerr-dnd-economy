namespace DndEconomy.Application.Pricing;

/// <summary>
/// Сервис расчёта текущей цены предмета с учётом активной игровой сессии, города и сезона.
/// Переносит логику листа "Текущая стоимость" исходной таблицы на бэкенд.
/// </summary>
public interface IPriceCalculationService
{
  /// <summary>
  /// Рассчитывает текущую цену покупки/продажи предмета на сегодняшнюю дату.
  /// </summary>
  /// <param name="itemId">Идентификатор предмета.</param>
  /// <param name="cancellationToken">Токен отмены.</param>
  /// <returns>Результат расчёта или null, если предмет не найден.</returns>
  Task<ItemPriceResult?> CalculateCurrentPriceAsync(Guid itemId, CancellationToken cancellationToken);
}
