using DndEconomy.Domain.Common;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Связь "игрок сохранил предмет в свой профиль" — используется для списка избранного
/// с ценой и весом, который игрок видит у себя в профиле.
/// </summary>
public class UserSavedItem : AuditableEntity
{
  /// <summary>Идентификатор пользователя-владельца записи.</summary>
  public Guid UserId { get; set; }

  /// <summary>Навигационное свойство на пользователя.</summary>
  public ApplicationUser? User { get; set; }

  /// <summary>Идентификатор сохранённого предмета.</summary>
  public Guid ItemId { get; set; }

  /// <summary>Навигационное свойство на предмет.</summary>
  public Item? Item { get; set; }

  /// <summary>Необязательная личная заметка игрока (например, "для крафта зелья").</summary>
  public string? Note { get; set; }
}
