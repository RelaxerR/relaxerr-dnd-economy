using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Requests;

/// <summary>Заявка игрока на добавление предмета — для отображения в профиле.</summary>
public sealed class ItemRequestSummary
{
  public required Guid Id { get; init; }
  public required string ProposedName { get; init; }
  public string? Description { get; init; }
  public required ItemRequestStatus Status { get; init; }
  public required DateTime CreatedAtUtc { get; init; }
  public string? ReviewComment { get; init; }
}
