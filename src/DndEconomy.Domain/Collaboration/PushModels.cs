namespace DndEconomy.Domain.Collaboration;

/// <summary>Подписка устройства, принадлежащая одному аккаунту; endpoint и ключи приватны.</summary>
public sealed class ChatPushSubscription
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UserId { get; set; }
  public string SecurityStamp { get; set; } = "";
  public string Endpoint { get; set; } = "";
  public string P256dh { get; set; } = "";
  public string Auth { get; set; } = "";
  public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Доставка на устройство с уникальным ключом уведомления и подписки.</summary>
public sealed class ChatPushDelivery
{
  public Guid NoticeId { get; set; }
  public Guid SubscriptionId { get; set; }
  public DateTime? SentUtc { get; set; }
  public int Attempts { get; set; }
  public DateTime NextAttemptUtc { get; set; }
}
