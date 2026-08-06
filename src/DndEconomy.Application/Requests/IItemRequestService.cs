namespace DndEconomy.Application.Requests;

/// <summary>Заявки игроков на добавление предметов. Рассмотрение (одобрение/отклонение) — Фаза 3.</summary>
public interface IItemRequestService
{
  Task SubmitAsync(Guid userId, string proposedName, string? description, CancellationToken cancellationToken);

  Task<IReadOnlyList<ItemRequestSummary>> GetMyRequestsAsync(Guid userId, CancellationToken cancellationToken);
}
