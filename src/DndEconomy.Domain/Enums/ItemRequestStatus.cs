namespace DndEconomy.Domain.Enums;

/// <summary>
/// Статус жизненного цикла заявки игрока на добавление нового предмета в каталог.
/// </summary>
public enum ItemRequestStatus
{
  /// <summary>Заявка отправлена и ожидает решения администратора.</summary>
  Pending = 0,

  /// <summary>Администратор одобрил заявку и создал предмет в каталоге.</summary>
  Approved = 1,

  /// <summary>Администратор отклонил заявку.</summary>
  Rejected = 2
}
