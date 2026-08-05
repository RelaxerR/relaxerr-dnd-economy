using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы модификаторов цены по сезону.</summary>
public class SeasonModifierConfiguration : IEntityTypeConfiguration<SeasonModifier>
{
  public void Configure(EntityTypeBuilder<SeasonModifier> builder)
  {
    builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
    builder.Property(x => x.Subtype).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Coefficient).HasPrecision(10, 4);

    // Один коэффициент на пару (Тип, Подтип, Сезон) — повторяет структуру матрицы листа "Сезонность".
    builder.HasIndex(x => new { x.Type, x.Subtype, x.Season }).IsUnique();
  }
}
