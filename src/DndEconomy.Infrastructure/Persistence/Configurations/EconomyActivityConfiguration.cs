using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Схема таблицы экономической активности. Без уникальных индексов по координатам — это
/// список ставок, а не матрица коэффициентов, и несколько строк с одинаковыми
/// Типом+Категорией+Уровнями — обычное дело. Инварианты сущности (опасность только у заданий,
/// корректные диапазоны уровней и ставок) закреплены CHECK-ограничениями, чтобы их нельзя было
/// обойти ни импортом, ни ручной правкой БД.
/// </summary>
public class EconomyActivityConfiguration : IEntityTypeConfiguration<EconomyActivity>
{
  public void Configure(EntityTypeBuilder<EconomyActivity> builder)
  {
    builder.Property(x => x.Category).HasMaxLength(200);
    builder.Property(x => x.RateMin).HasPrecision(18, 2);
    builder.Property(x => x.RateMax).HasPrecision(18, 2);
    builder.HasIndex(x => new { x.ActivityType, x.MinLevel, x.Category });

    builder.ToTable(table =>
    {
      table.HasCheckConstraint(
        "CK_EconomyActivities_DangerLevelOnlyForQuest",
        $"(\"ActivityType\" = {(int)EconomyActivityType.Quest}) = (\"DangerLevel\" IS NOT NULL)");
      table.HasCheckConstraint(
        "CK_EconomyActivities_LevelRange",
        "\"MinLevel\" >= 1 AND \"MinLevel\" <= \"MaxLevel\" AND \"MaxLevel\" <= 20");
      table.HasCheckConstraint("CK_EconomyActivities_RateRange", "\"RateMin\" <= \"RateMax\"");
      table.HasCheckConstraint("CK_EconomyActivities_DurationPositive", "\"RecommendedDurationDays\" >= 1");
    });
  }
}
