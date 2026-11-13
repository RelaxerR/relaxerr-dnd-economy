using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DndEconomy.Web;

/// <summary>EF tooling без запуска hosted services, миграций на старте и Identity сидинга.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
  /// <summary>Скрипты и новые миграции не требуют подключения; update использует явную переменную окружения.</summary>
  public ApplicationDbContext CreateDbContext(string[] args)
  {
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
      ?? "Host=localhost;Database=dnd_economy_design_only;Username=design";
    return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
  }
}
