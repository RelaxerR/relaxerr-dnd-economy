using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Заявка игрока на добавление предмета, который не нашёлся в поиске. Проходит через
/// статусы Pending → Approved/Rejected; при одобрении админ создаёт связанный Item и
/// заявка получает ссылку на него через ResultingItemId.
/// </summary>
public class ItemRequest : AuditableEntity
{
  #region Заявитель и содержание запроса

  /// <summary>Идентификатор игрока, отправившего заявку.</summary>
  public Guid RequestedByUserId { get; set; }

  /// <summary>Навигационное свойство на заявителя.</summary>
  public ApplicationUser? RequestedByUser { get; set; }

  /// <summary>Предложенное игроком название предмета.</summary>
  public string ProposedName { get; set; } = string.Empty;

  /// <summary>Свободное описание/контекст от игрока — зачем нужен предмет, откуда он (книга, сессия).</summary>
  public string? Description { get; set; }

  #endregion

  #region Рассмотрение администратором

  /// <summary>Текущий статус заявки.</summary>
  public ItemRequestStatus Status { get; set; } = ItemRequestStatus.Pending;

  /// <summary>Идентификатор администратора, рассмотревшего заявку. Null, пока заявка в статусе Pending.</summary>
  public Guid? ReviewedByUserId { get; set; }

  /// <summary>Навигационное свойство на администратора-рецензента.</summary>
  public ApplicationUser? ReviewedByUser { get; set; }

  /// <summary>Дата и время рассмотрения заявки (UTC).</summary>
  public DateTime? ReviewedAtUtc { get; set; }

  /// <summary>Комментарий администратора (например, причина отклонения).</summary>
  public string? ReviewComment { get; set; }

  #endregion

  #region Результат

  /// <summary>Идентификатор предмета, созданного при одобрении заявки. Null, если заявка ещё не одобрена.</summary>
  public Guid? ResultingItemId { get; set; }

  /// <summary>Навигационное свойство на созданный предмет.</summary>
  public Item? ResultingItem { get; set; }

  #endregion
}
