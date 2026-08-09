using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DndEconomy.Infrastructure.Persistence;

/// <summary>
/// Применяет ожидающие EF-миграции при старте приложения. Нужно для деплоя в Docker —
/// там нет отдельного шага "выполнить миграции", схема БД в свежем контейнере Postgres
/// иначе просто не появится.
/// </summary>
public static class DatabaseMigrationExtensions
{
  #region Публичная точка входа

  /// <summary>Открывает короткоживущий scope и применяет все не применённые миграции.</summary>
  public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
  {
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync(cancellationToken);
  }

  #endregion
}
