using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Persistence;

/// <summary>
/// Основной контекст базы данных. Наследуется от IdentityDbContext, чтобы таблицы
/// пользователей/ролей жили в той же БД и той же схеме миграций, что и доменные сущности.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
  {
  }

  #region Наборы сущностей

  public DbSet<Item> Items => Set<Item>();
  public DbSet<City> Cities => Set<City>();
  public DbSet<CityModifier> CityModifiers => Set<CityModifier>();
  public DbSet<SeasonModifier> SeasonModifiers => Set<SeasonModifier>();
  public DbSet<EconomySession> EconomySessions => Set<EconomySession>();
  public DbSet<UserSavedItem> UserSavedItems => Set<UserSavedItem>();
  public DbSet<ItemRequest> ItemRequests => Set<ItemRequest>();
  public DbSet<Notification> Notifications => Set<Notification>();

  #endregion

  /// <summary>
  /// Применяет все конфигурации сущностей (IEntityTypeConfiguration&lt;T&gt;) из текущей сборки,
  /// чтобы не собирать Fluent API конфигурацию монолитным методом.
  /// </summary>
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Нужно для опечатко-устойчивого поиска по каталогу (GIN-индексы gin_trgm_ops в ItemConfiguration).
    modelBuilder.HasPostgresExtension("pg_trgm");

    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
  }
}
