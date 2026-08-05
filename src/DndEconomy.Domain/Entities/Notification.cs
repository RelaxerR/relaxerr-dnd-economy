using DndEconomy.Domain.Common;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Уведомление пользователю (игроку или админу) — заявка отправлена/одобрена/отклонена,
/// предмет добавлен и т.д. Отображается в профиле и толкается в реальном времени через SignalR.
/// </summary>
public class Notification : AuditableEntity
{
  #region Получатель

  /// <summary>Идентификатор пользователя-получателя.</summary>
  public Guid UserId { get; set; }

  /// <summary>Навигационное свойство на получателя.</summary>
  public ApplicationUser? User { get; set; }

  #endregion

  #region Содержание

  /// <summary>Тип уведомления — определяет иконку/стиль на клиенте.</summary>
  public NotificationType Type { get; set; }

  /// <summary>Короткий заголовок.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Текст уведомления.</summary>
  public string Message { get; set; } = string.Empty;

  #endregion

  #region Связанные сущности

  /// <summary>Ссылка на связанный предмет, если применимо.</summary>
  public Guid? RelatedItemId { get; set; }

  /// <summary>Ссылка на связанную заявку, если применимо.</summary>
  public Guid? RelatedItemRequestId { get; set; }

  #endregion

  #region Статус прочтения

  /// <summary>Признак того, что пользователь открыл/прочитал уведомление.</summary>
  public bool IsRead { get; set; }

  /// <summary>Дата прочтения (UTC).</summary>
  public DateTime? ReadAtUtc { get; set; }

  #endregion
}
