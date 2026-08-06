namespace DndEconomy.Application.Items;

/// <summary>Создание предметов каталога админом (вручную или при одобрении заявки игрока).</summary>
public interface IItemAdminService
{
  Task<Guid> CreateItemAsync(NewItemInput input, CancellationToken cancellationToken);
}
