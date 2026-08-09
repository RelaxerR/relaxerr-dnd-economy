namespace DndEconomy.Application.Users;

/// <summary>
/// Управление пользователями из админ-панели — это и есть "приглашение" (Фаза 3): админ
/// создаёт учётку с временным паролем и передаёт его игроку вручную (Discord/лично),
/// самостоятельной регистрации нет.
/// </summary>
public interface IAdminUserService
{
  Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Создаёт пользователя с временным паролем (MustChangePassword=true) и указанной ролью
  /// (<see cref="DndEconomy.Domain.Constants.RoleNames"/>.Admin или .Player). Возвращает ошибки
  /// Identity, если есть.
  /// </summary>
  Task<IReadOnlyList<string>> InviteUserAsync(
    Guid invitedByUserId, string email, string displayName, string temporaryPassword, string role, CancellationToken cancellationToken);

  /// <summary>
  /// Удаляет пользователя безвозвратно. Отклоняется (с сообщением об ошибке), если админ пытается
  /// удалить сам себя или последнего оставшегося администратора — иначе можно случайно остаться
  /// без доступа в админку.
  /// </summary>
  Task<IReadOnlyList<string>> DeleteUserAsync(Guid requestingAdminId, Guid userId, CancellationToken cancellationToken);
}
