namespace DndEconomy.Domain.Collaboration;

/// <summary>Чат кампании или ветка с неизменяемой областью доступа.</summary>
public sealed class ChatRoom
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Name { get; set; } = "";
  public bool IsMain { get; set; }
  public Guid? ParentMessageId { get; set; }
  public bool IsThread { get; set; }
  public Guid[] Readers { get; set; } = [];
  public DateTime ActivityUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Сообщение, включая личное, с независимой областью доступа.</summary>
public sealed class ChatMessage
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RoomId { get; set; }
  public Guid AuthorId { get; set; }
  public Guid[] Readers { get; set; } = [];
  public string Text { get; set; } = "";
  public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
  public DateTime? EditedUtc { get; set; }
  public bool Highlighted { get; set; }
  public DateTime? ConfirmedTimeUtc { get; set; }
  public string? SourceZone { get; set; }
}

/// <summary>Прочтение отдельного доступного сообщения.</summary>
public sealed class ChatRead
{
  public Guid MessageId { get; set; }
  public Guid UserId { get; set; }
  public DateTime ReadUtc { get; set; }
}

/// <summary>Приватный файл; ключ хранилища не зависит от имени пользователя.</summary>
public sealed class ChatAttachment
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid MessageId { get; set; }
  public string Name { get; set; } = "";
  public long Length { get; set; }
  public DateTime ExpiresUtc { get; set; }
}

/// <summary>Обычный опрос в сообщении.</summary>
public sealed class ChatPoll
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid MessageId { get; set; }
  public bool Multiple { get; set; }
  public DateTime EndsUtc { get; set; }
}

/// <summary>Вариант опроса.</summary>
public sealed class PollOption
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PollId { get; set; }
  public Guid AuthorId { get; set; }
  public string Text { get; set; } = "";
}

/// <summary>Голос пользователя за вариант.</summary>
public sealed class PollVote
{
  public Guid OptionId { get; set; }
  public Guid UserId { get; set; }
}

/// <summary>Настройки отображения и типов уведомлений пользователя.</summary>
public sealed class ChatPreference
{
  public Guid UserId { get; set; }
  public string Color { get; set; } = "#8b4513";
  public string Zone { get; set; } = "Europe/Samara";
  public string[] Events { get; set; } = ["message", "private", "thread", "mention", "poll", "request", "assigned", "cancelled", "reminder"];
}

/// <summary>Активность вкладки, истекающая после потери соединения.</summary>
public sealed class ChatPresence
{
  public string ConnectionId { get; set; } = "";
  public Guid UserId { get; set; }
  public Guid RoomId { get; set; }
  public DateTime SeenUtc { get; set; }
  public DateTime TypingUntilUtc { get; set; }
}

/// <summary>Долговечное уведомление с дедупликацией и ссылкой на область доступа.</summary>
public sealed class ChatNotice
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UserId { get; set; }
  public Guid RoomId { get; set; }
  public Guid? MessageId { get; set; }
  public string EventKey { get; set; } = "";
  public string Kind { get; set; } = "";
  public string Title { get; set; } = "";
  public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
  public bool Read { get; set; }
}
