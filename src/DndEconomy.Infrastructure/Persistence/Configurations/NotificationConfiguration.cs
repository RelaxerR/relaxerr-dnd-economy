using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DndEconomy.Infrastructure.Persistence.Configurations;

/// <summary>Схема таблицы уведомлений.</summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
  public void Configure(EntityTypeBuilder<Notification> builder)
  {
    builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
    builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();

    builder.HasOne(x => x.User)
      .WithMany(x => x.Notifications)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    // Профиль запрашивает "непрочитанные уведомления пользователя" — составной индекс под этот запрос.
    builder.HasIndex(x => new { x.UserId, x.IsRead });
  }
}
