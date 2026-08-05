using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы заявок игроков на добавление предметов.</summary>
public class ItemRequestConfiguration : IEntityTypeConfiguration<ItemRequest>
{
  public void Configure(EntityTypeBuilder<ItemRequest> builder)
  {
    builder.Property(x => x.ProposedName).HasMaxLength(300).IsRequired();
    builder.Property(x => x.Description).HasMaxLength(2000);
    builder.Property(x => x.ReviewComment).HasMaxLength(1000);

    builder.HasOne(x => x.RequestedByUser)
      .WithMany(x => x.SubmittedRequests)
      .HasForeignKey(x => x.RequestedByUserId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(x => x.ReviewedByUser)
      .WithMany()
      .HasForeignKey(x => x.ReviewedByUserId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(x => x.ResultingItem)
      .WithMany()
      .HasForeignKey(x => x.ResultingItemId)
      .OnDelete(DeleteBehavior.SetNull);

    // Список заявок в админке фильтруется по статусу — индекс ускоряет выборку "Pending".
    builder.HasIndex(x => x.Status);
  }
}
