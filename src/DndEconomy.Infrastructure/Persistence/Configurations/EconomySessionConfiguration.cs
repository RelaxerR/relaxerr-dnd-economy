using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы игровых сессий ("Партий").</summary>
public class EconomySessionConfiguration : IEntityTypeConfiguration<EconomySession>
{
  public void Configure(EntityTypeBuilder<EconomySession> builder)
  {
    builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
    builder.Property(x => x.GameDateLabel).HasMaxLength(100);
    builder.Property(x => x.BaseCoefficient).HasPrecision(10, 4);
    builder.Property(x => x.SellCoefficient).HasPrecision(10, 4);

    builder.HasOne(x => x.City)
      .WithMany()
      .HasForeignKey(x => x.CityId)
      .OnDelete(DeleteBehavior.SetNull);

    // Расчёт цены ищет ближайшую по дате сессию — индекс критичен для производительности запроса.
    builder.HasIndex(x => x.RealDate);
  }
}
