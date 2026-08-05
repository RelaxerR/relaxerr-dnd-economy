using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы модификаторов цены по городу.</summary>
public class CityModifierConfiguration : IEntityTypeConfiguration<CityModifier>
{
  public void Configure(EntityTypeBuilder<CityModifier> builder)
  {
    builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
    builder.Property(x => x.Subtype).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Coefficient).HasPrecision(10, 4);

    // Один коэффициент на пару (Тип, Подтип, Город) — повторяет структуру матрицы листа "Города".
    builder.HasIndex(x => new { x.Type, x.Subtype, x.CityId }).IsUnique();
  }
}
