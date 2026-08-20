namespace DndEconomy.Application.Items;

/// <summary>
/// Создание предметов каталога админом (вручную или при одобрении заявки игрока) и глобальное
/// управление их стоимостью (массовое изменение BaseCost по условию — см. AdminItemsBulkPrice.razor).
/// </summary>
public interface IItemAdminService
{
  Task<Guid> CreateItemAsync(NewItemInput input, CancellationToken cancellationToken);

  /// <summary>
  /// Считает предметы, попадающие под <paramref name="input"/>.Filter, и стоимость, которая
  /// получится после применения операции — ничего не сохраняет. Для предпросмотра перед
  /// необратимым массовым изменением.
  /// </summary>
  Task<IReadOnlyList<BulkPriceUpdatePreviewRow>> PreviewBulkPriceUpdateAsync(
    BulkPriceUpdateInput input, CancellationToken cancellationToken);

  /// <summary>
  /// Применяет массовое изменение BaseCost ко всем предметам, попадающим под условие отбора.
  /// Возвращает количество изменённых предметов.
  /// </summary>
  Task<int> ApplyBulkPriceUpdateAsync(BulkPriceUpdateInput input, CancellationToken cancellationToken);
}
