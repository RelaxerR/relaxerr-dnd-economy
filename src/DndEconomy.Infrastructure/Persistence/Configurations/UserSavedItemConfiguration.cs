using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы сохранённых игроками предметов.</summary>
public class UserSavedItemConfiguration : IEntityTypeConfiguration<UserSavedItem>
{
  public void Configure(EntityTypeBuilder<UserSavedItem> builder)
  {
    builder.Property(x => x.Note).HasMaxLength(500);

    builder.HasOne(x => x.User)
      .WithMany(x => x.SavedItems)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(x => x.Item)
      .WithMany(x => x.SavedByUsers)
      .HasForeignKey(x => x.ItemId)
      .OnDelete(DeleteBehavior.Cascade);

    // Игрок не может сохранить один и тот же предмет дважды.
    builder.HasIndex(x => new { x.UserId, x.ItemId }).IsUnique();
  }
}
