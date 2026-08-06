namespace DndEconomy.Application.Users;

/// <summary>
/// Управление пользователями из админ-панели — это и есть "приглашение" (Фаза 3): админ
/// создаёт учётку с временным паролем и передаёт его игроку вручную (Discord/лично),
/// самостоятельной регистрации нет.
/// </summary>
public interface IAdminUserService
{
  Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken);

  /// <summary>Создаёт игрока с временным паролем (MustChangePassword=true). Возвращает ошибки Identity, если есть.</summary>
  Task<IReadOnlyList<string>> InviteUserAsync(
    Guid invitedByUserId, string email, string displayName, string temporaryPassword, CancellationToken cancellationToken);
}
