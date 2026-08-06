using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Economy;

/// <summary>
/// Управление городами и экономическими сессиями из админ-панели. Коэффициенты по
/// (Тип, Подтип) — CityModifier/SeasonModifier — заводятся через импорт Excel
/// (см. страницу /admin/import), отдельный UI-редактор матрицы коэффициентов не сделан
/// (не нужен при малом числе городов/сезонов — правки проще внести в исходную таблицу).
/// </summary>
public interface IEconomyAdminService
{
  Task<IReadOnlyList<CitySummary>> GetCitiesAsync(CancellationToken cancellationToken);

  Task<Guid> CreateCityAsync(string name, CitySize size, CancellationToken cancellationToken);

  Task<IReadOnlyList<EconomySessionSummary>> GetSessionsAsync(CancellationToken cancellationToken);

  Task CreateSessionAsync(NewEconomySessionInput input, CancellationToken cancellationToken);
}
