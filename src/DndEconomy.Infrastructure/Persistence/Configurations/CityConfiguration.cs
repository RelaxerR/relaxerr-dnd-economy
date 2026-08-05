using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы городов.</summary>
public class CityConfiguration : IEntityTypeConfiguration<City>
{
  public void Configure(EntityTypeBuilder<City> builder)
  {
    builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
    builder.HasIndex(x => x.Name).IsUnique();

    builder.HasMany(x => x.Modifiers)
      .WithOne(x => x.City)
      .HasForeignKey(x => x.CityId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
