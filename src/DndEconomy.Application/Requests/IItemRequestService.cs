using DndEconomy.Application.Items;

namespace DndEconomy.Application.Requests;

/// <summary>Заявки игроков на добавление предметов и их рассмотрение админом.</summary>
public interface IItemRequestService
{
  Task SubmitAsync(Guid userId, string proposedName, string? description, CancellationToken cancellationToken);

  Task<IReadOnlyList<ItemRequestSummary>> GetMyRequestsAsync(Guid userId, CancellationToken cancellationToken);

  Task<IReadOnlyList<ItemRequestSummary>> GetPendingAsync(CancellationToken cancellationToken);

  /// <summary>Одобряет заявку: создаёт предмет по <paramref name="itemInput"/>, связывает его
  /// с заявкой и уведомляет заявителя.</summary>
  Task ApproveAsync(Guid requestId, Guid adminUserId, NewItemInput itemInput, CancellationToken cancellationToken);

  /// <summary>Отклоняет заявку с комментарием и уведомляет заявителя.</summary>
  Task RejectAsync(Guid requestId, Guid adminUserId, string comment, CancellationToken cancellationToken);
}
