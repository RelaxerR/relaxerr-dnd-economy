using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Схема таблицы оплаты заданий. Без уникальных индексов по координатам — в отличие от
/// CityModifier/SeasonModifier это не матрица коэффициентов, а список заданий, и несколько
/// строк с одинаковой Категорией+Опасностью+Эпохой — обычное дело.
/// </summary>
public class QuestPayRateConfiguration : IEntityTypeConfiguration<QuestPayRate>
{
  public void Configure(EntityTypeBuilder<QuestPayRate> builder)
  {
    builder.Property(x => x.Category).HasMaxLength(200);
    builder.Property(x => x.Duration).HasMaxLength(100);
    builder.HasIndex(x => new { x.Epoch, x.Category, x.DangerLevel });
  }
}
