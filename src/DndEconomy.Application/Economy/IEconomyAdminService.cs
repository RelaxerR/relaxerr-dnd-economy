using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Economy;

/// <summary>
/// Управление городами, экономическими сессиями и коэффициентами (Тип+Подтип × Город/Сезон)
/// из админ-панели. Коэффициенты по-прежнему можно завести и через импорт Excel
/// (см. страницу /admin/import) — оба пути пишут в одни и те же таблицы CityModifier/
/// SeasonModifier, поэтому взаимозаменяемы и не конфликтуют.
/// </summary>
public interface IEconomyAdminService
{
  Task<IReadOnlyList<CitySummary>> GetCitiesAsync(CancellationToken cancellationToken);

  Task<Guid> CreateCityAsync(string name, CitySize size, CancellationToken cancellationToken);

  Task<IReadOnlyList<EconomySessionSummary>> GetSessionsAsync(CancellationToken cancellationToken);

  Task CreateSessionAsync(NewEconomySessionInput input, CancellationToken cancellationToken);

  /// <summary>Обновляет существующую сессию (редактирование из карточки в админке).</summary>
  Task UpdateSessionAsync(Guid sessionId, NewEconomySessionInput input, CancellationToken cancellationToken);

  /// <summary>Удаляет сессию безвозвратно.</summary>
  Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken);

  /// <summary>
  /// Закрепляет сессию как принудительно показываемую игрокам (в обход обычного выбора
  /// "последняя сессия с RealDate не позже сегодня"), либо снимает закрепление, если
  /// <paramref name="sessionId"/> равен null — тогда снова используется выбор по дате.
  /// Закрепление автоматически перестаёт действовать, как только у другой сессии наступает
  /// более поздняя RealDate (см. EconomyPricingReadStore.GetActiveSessionContextAsync).
  /// </summary>
  Task SetDisplaySessionOverrideAsync(Guid? sessionId, CancellationToken cancellationToken);

  #region Коэффициенты города/сезона

  /// <summary>Различные пары Тип+Подтип, встречающиеся у предметов каталога — подсказки для формы добавления строки.</summary>
  Task<IReadOnlyList<TypeSubtype>> GetItemTypeSubtypesAsync(CancellationToken cancellationToken);

  /// <summary>Полная матрица коэффициентов "Тип+Подтип × Город" для редактора в админке.</summary>
  Task<CityModifierMatrix> GetCityModifierMatrixAsync(CancellationToken cancellationToken);

  /// <summary>Создаёт или обновляет коэффициент для одной ячейки матрицы (Тип+Подтип, город).</summary>
  Task SetCityModifierAsync(string type, string subtype, Guid cityId, decimal coefficient, CancellationToken cancellationToken);

  /// <summary>Удаляет всю строку матрицы (коэффициенты этого Типа+Подтипа для всех городов сразу).</summary>
  Task DeleteCityModifierRowAsync(string type, string subtype, CancellationToken cancellationToken);

  /// <summary>Полная матрица коэффициентов "Тип+Подтип × Сезон" для редактора в админке.</summary>
  Task<IReadOnlyList<SeasonModifierMatrixRow>> GetSeasonModifierMatrixAsync(CancellationToken cancellationToken);

  /// <summary>Создаёт или обновляет коэффициент для одной ячейки матрицы (Тип+Подтип, сезон).</summary>
  Task SetSeasonModifierAsync(string type, string subtype, Season season, decimal coefficient, CancellationToken cancellationToken);

  /// <summary>Удаляет всю строку матрицы (коэффициенты этого Типа+Подтипа для всех сезонов сразу).</summary>
  Task DeleteSeasonModifierRowAsync(string type, string subtype, CancellationToken cancellationToken);

  #endregion
}
