using Microsoft.AspNetCore.Identity;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Пользователь сайта. Расширяет стандартного IdentityUser полями, нужными для закрытой
/// (по приглашениям) регистрации: кто пригласил, должен ли пользователь сменить пароль,
/// и отображаемое игровое имя.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
  #region Профиль

  /// <summary>
  /// Отображаемое имя (имя персонажа/игрока), показывается вместо логина в интерфейсе.
  /// </summary>
  public string DisplayName { get; set; } = string.Empty;

  #endregion

  #region Приглашение и первый вход

  /// <summary>
  /// Идентификатор администратора, создавшего учётку приглашением. Null для самой первой
  /// учётки-суперадмина, созданной при развёртывании.
  /// </summary>
  public Guid? InvitedByUserId { get; set; }

  /// <summary>
  /// Признак того, что пользователь ещё не сменил временный пароль, выданный при приглашении.
  /// Пока true — при входе принудительно показываем экран смены пароля.
  /// </summary>
  public bool MustChangePassword { get; set; } = true;

  #endregion

  #region Аудит

  /// <summary>Дата создания учётки (UTC).</summary>
  public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

  /// <summary>Дата последнего успешного входа (UTC). Null, если пользователь ещё ни разу не заходил.</summary>
  public DateTime? LastLoginAtUtc { get; set; }

  #endregion

  #region Навигационные свойства

  /// <summary>Предметы, сохранённые пользователем в свой профиль.</summary>
  public ICollection<UserSavedItem> SavedItems { get; set; } = new List<UserSavedItem>();

  /// <summary>Заявки на добавление предметов, поданные этим пользователем.</summary>
  public ICollection<ItemRequest> SubmittedRequests { get; set; } = new List<ItemRequest>();

  /// <summary>Уведомления, адресованные этому пользователю.</summary>
  public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

  #endregion
}
