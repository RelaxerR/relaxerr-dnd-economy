namespace DndEconomy.Application.QuestPay;

/// <summary>
/// CRUD над справочником оплаты заданий (Категория × Опасность × Эпоха → оплата партии) —
/// admin-only инструмент подготовки мастера к сессии, независимый от расчёта цены предметов
/// (PriceCalculationService/CityModifier и т.д. эту таблицу не читают).
/// </summary>
public interface IQuestPayRateAdminService
{
  Task<IReadOnlyList<QuestPayRateSummary>> GetAllAsync(CancellationToken cancellationToken);

  Task<Guid> CreateAsync(NewQuestPayRateInput input, CancellationToken cancellationToken);

  Task UpdateAsync(Guid id, NewQuestPayRateInput input, CancellationToken cancellationToken);

  Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
