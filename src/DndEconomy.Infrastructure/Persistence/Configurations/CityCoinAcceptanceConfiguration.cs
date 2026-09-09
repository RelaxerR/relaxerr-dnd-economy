using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы условий приёма номиналов монет по городу.</summary>
public class CityCoinAcceptanceConfiguration : IEntityTypeConfiguration<CityCoinAcceptance>
{
  public void Configure(EntityTypeBuilder<CityCoinAcceptance> builder)
  {
    builder.Property(x => x.AcceptanceRate).HasPrecision(10, 4);

    // Один номинал — одна запись на город, как и у CityModifier (Type+Subtype+Город).
    builder.HasIndex(x => new { x.CityId, x.Denomination }).IsUnique();
  }
}
