namespace DndEconomy.Domain.Enums;

/// <summary>
/// Тип уведомления. Используется для выбора иконки/стиля на клиенте и для фильтрации в профиле.
/// </summary>
public enum NotificationType
{
  /// <summary>Игрок отправил новую заявку на добавление предмета (уведомление админу).</summary>
  ItemRequestSubmitted = 0,

  /// <summary>Заявка игрока одобрена, предмет добавлен в каталог (уведомление игроку).</summary>
  ItemRequestApproved = 1,

  /// <summary>Заявка игрока отклонена (уведомление игроку).</summary>
  ItemRequestRejected = 2,

  /// <summary>Администратор добавил предмет напрямую, без предварительной заявки.</summary>
  ItemAddedByAdmin = 3,

  /// <summary>Системное уведомление общего назначения.</summary>
  System = 4
}
