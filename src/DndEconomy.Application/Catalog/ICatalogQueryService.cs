namespace DndEconomy.Application.Catalog;

/// <summary>Публичный порт каталога для Web-слоя — прячет за собой контекст активной сессии и bulk-расчёт цены.</summary>
public interface ICatalogQueryService
{
  Task<CatalogPage> GetPageAsync(CatalogQuery query, CancellationToken cancellationToken);

  Task<CatalogItemViewModel?> GetItemAsync(Guid itemId, CancellationToken cancellationToken);
}
