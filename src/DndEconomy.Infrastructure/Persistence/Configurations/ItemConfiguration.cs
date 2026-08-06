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
    // Коллация ru-x-icu (ICU, идёт в поставке Postgres — не зависит от локалей ОС, в отличие
    // от glibc-локали ru_RU.UTF-8) — без неё сортировка "по алфавиту" на сервере с локалью
    // по умолчанию (например en_US) даёт визуально случайный порядок кириллицы, а не А-Я.
    builder.Property(x => x.NameRu).HasMaxLength(300).IsRequired().UseCollation("ru-x-icu");
    builder.Property(x => x.NameEn).HasMaxLength(300);
    builder.Property(x => x.BaseCost).HasPrecision(18, 2);
    builder.Property(x => x.Weight).HasPrecision(10, 2);
    builder.Property(x => x.ExternalUuid).HasMaxLength(100);

    // Основной индекс для быстрой фильтрации по категориям в каталоге.
    builder.HasIndex(x => new { x.Type, x.Subtype });

    // Опечатко-устойчивый поиск (Postgres pg_trgm, см. ApplicationDbContext.OnModelCreating).
    // CatalogReadStore.BuildFilterPredicate сравнивает word_similarity(...) с явным порогом
    // в коде (а не через оператор `%`/`<%` с GUC-порогом) — поэтому сам поиск сейчас идёт полным
    // сканом, индекс его не ускоряет. GIN с gin_trgm_ops оставлен на будущее (тот же индекс
    // ускоряет ILIKE '%...%' и точный оператор `%`/`<%`, если поисковый запрос усложнится), и
    // почти не стоит ничего при текущем масштабе каталога (сотни строк, редкие вставки).
    builder.HasIndex(x => x.NameRu).HasMethod("gin").HasOperators("gin_trgm_ops");
    builder.HasIndex(x => x.NameEn).HasMethod("gin").HasOperators("gin_trgm_ops");
  }
}
