using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы предметов каталога и её индексы.</summary>
public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
  public void Configure(EntityTypeBuilder<Item> builder)
  {
    builder.Property(x => x.Category).HasMaxLength(200).IsRequired();
    builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
    builder.Property(x => x.Subtype).HasMaxLength(150).IsRequired();
    builder.Property(x => x.NameRu).HasMaxLength(300).IsRequired();
    builder.Property(x => x.NameEn).HasMaxLength(300);
    builder.Property(x => x.BaseCost).HasPrecision(18, 2);
    builder.Property(x => x.Weight).HasPrecision(10, 2);
    builder.Property(x => x.ExternalUuid).HasMaxLength(100);

    // Основной индекс для быстрой фильтрации по категориям в каталоге.
    builder.HasIndex(x => new { x.Type, x.Subtype });

    // Полнотекстовый поиск по русскому и английскому названию настраивается миграцией
    // (генерируемая колонка tsvector + GIN-индекс, добавляется отдельной SQL-миграцией в Фазе 1).
    builder.HasIndex(x => x.NameRu);
  }
}
