using DndEconomy.Domain.Collaboration;
using DndEconomy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Ограничения и индексы модуля; старые таблицы и строки не заменяются.</summary>
public static class CollaborationSchema
{
  /// <summary>Настраивает внешние ключи, каскады и уникальность идемпотентных операций.</summary>
  public static void Configure(ModelBuilder b)
  {
    b.Entity<ChatPushSubscription>().HasKey(x => x.Id);
    b.Entity<ChatPushSubscription>().Property(x => x.Endpoint).HasMaxLength(2048);
    b.Entity<ChatPushSubscription>().HasIndex(x => x.Endpoint).IsUnique();
    b.Entity<ChatPushDelivery>().HasKey(x => new { x.NoticeId, x.SubscriptionId });
    var room = b.Entity<ChatRoom>();
    room.HasKey(x => x.Id);
    room.Property(x => x.Name).HasMaxLength(200);
    room.HasIndex(x => x.IsMain).IsUnique().HasFilter("\"IsMain\"");
    room.HasIndex(x => x.ParentMessageId).IsUnique();
    room.HasIndex(x => x.ActivityUtc);
    // ParentMessageId намеренно без FK: удаление корня сохраняет ветку и её права.
    var message = b.Entity<ChatMessage>();
    message.HasKey(x => x.Id);
    message.Property(x => x.Text).HasMaxLength(12000);
    message.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    message.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
    message.HasIndex(x => new { x.RoomId, x.CreatedUtc, x.Id });
    var read = b.Entity<ChatRead>();
    read.HasKey(x => new { x.MessageId, x.UserId });
    read.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    var attachment = b.Entity<ChatAttachment>();
    attachment.HasKey(x => x.Id);
    attachment.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    attachment.Property(x => x.Name).HasMaxLength(255);
    attachment.HasIndex(x => x.ExpiresUtc);
    var poll = b.Entity<ChatPoll>();
    poll.HasKey(x => x.Id);
    poll.HasIndex(x => x.MessageId).IsUnique();
    poll.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    var option = b.Entity<PollOption>();
    option.HasKey(x => x.Id);
    option.Property(x => x.Text).HasMaxLength(500);
    option.HasOne<ChatPoll>().WithMany().HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
    var vote = b.Entity<PollVote>();
    vote.HasKey(x => new { x.OptionId, x.UserId });
    vote.HasOne<PollOption>().WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Cascade);
    b.Entity<ChatPreference>().HasKey(x => x.UserId);
    b.Entity<ChatPreference>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    b.Entity<ChatPresence>().HasKey(x => x.ConnectionId);
    b.Entity<ChatPresence>().HasIndex(x => x.SeenUtc);
    b.Entity<ChatNotice>().HasKey(x => x.Id);
    b.Entity<ChatNotice>().HasIndex(x => new { x.UserId, x.EventKey }).IsUnique();
    var rule = b.Entity<AvailabilityRule>();
    rule.HasKey(x => x.Id);
    rule.HasIndex(x => x.UserId);
    rule.Property(x => x.LocalStart).HasColumnType("timestamp without time zone");
    rule.Property(x => x.LocalEnd).HasColumnType("timestamp without time zone");
    rule.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    var request = b.Entity<SchedulingRequest>();
    request.HasKey(x => x.Id);
    request.Property(x => x.SettingsJson).HasColumnType("jsonb");
    request.Property(x => x.OptionsJson).HasColumnType("jsonb");
    request.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<SchedulingBallot>().HasKey(x => new { x.RequestId, x.Round, x.UserId });
    b.Entity<SchedulingBallot>().HasOne<SchedulingRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    var game = b.Entity<ScheduledGame>();
    game.HasKey(x => x.Id);
    game.HasIndex(x => new { x.RequestId, x.Round }).IsUnique();
    game.HasIndex(x => x.EconomySessionId).IsUnique();
    game.HasOne<SchedulingRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    game.HasOne<EconomySession>().WithMany().HasForeignKey(x => x.EconomySessionId).OnDelete(DeleteBehavior.Restrict);
  }
}
