namespace DndEconomy.Application.Activities;

/// <summary>
/// CRUD над справочником экономической активности (ставки дохода/расхода в зм/день на игрока
/// для партийных заданий и занятий в простое) — admin-only инструмент мастера, независимый от
/// расчёта цены предметов (PriceCalculationService/CityModifier и т.д. эту таблицу не читают).
/// Create/Update бросают <see cref="ArgumentException"/>, если ввод нарушает инварианты
/// сущности (см. <see cref="EconomyActivityValidator"/>).
/// </summary>
public interface IEconomyActivityAdminService
{
  Task<IReadOnlyList<EconomyActivitySummary>> GetAllAsync(CancellationToken cancellationToken);

  Task<Guid> CreateAsync(NewEconomyActivityInput input, CancellationToken cancellationToken);

  Task UpdateAsync(Guid id, NewEconomyActivityInput input, CancellationToken cancellationToken);

  Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
