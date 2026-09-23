using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы коэффициентов размера партии — одна строка на размер партии.</summary>
public class PartySizeCoefficientConfiguration : IEntityTypeConfiguration<PartySizeCoefficient>
{
  public void Configure(EntityTypeBuilder<PartySizeCoefficient> builder)
  {
    builder.Property(x => x.Coefficient).HasPrecision(10, 4);
    builder.HasIndex(x => x.PartySize).IsUnique();

    builder.ToTable(table =>
    {
      table.HasCheckConstraint(
        "CK_PartySizeCoefficients_PartySizeRange",
        $"\"PartySize\" >= {PartySizeCoefficient.MinPartySize} AND \"PartySize\" <= {PartySizeCoefficient.MaxPartySize}");
      table.HasCheckConstraint("CK_PartySizeCoefficients_CoefficientPositive", "\"Coefficient\" > 0");
    });
  }
}
