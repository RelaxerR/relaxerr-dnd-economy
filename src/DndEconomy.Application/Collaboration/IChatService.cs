using System.Security.Claims;
using DndEconomy.Domain.Collaboration;

namespace DndEconomy.Application.Collaboration;

/// <summary>Отображаемое имя и безопасный CSS-цвет.</summary>
public sealed record ChatPerson(Guid Id, string Name, string Color);
/// <summary>Сообщение со всеми доступными текущему читателю метаданными.</summary>
public sealed record MessageView(ChatMessage Message, List<Guid> ReadBy, List<ChatAttachment> Attachments,
  ChatPoll? Poll, List<PollOption> Options, List<PollVote> Votes);
/// <summary>Снимок комнаты. Повторное чтение восстанавливает изменения и удаления после reconnect.</summary>
public sealed record ChatSnapshot(ChatRoom CurrentRoom, List<ChatRoom> Rooms, List<MessageView> Messages, List<ChatMessage> Pinned, List<ChatPerson> People,
  List<Guid> Online, List<Guid> Typing, List<SchedulingRequest> Requests, List<ScheduledGame> Games, List<SchedulingBallot> Ballots)
{
  /// <summary>Непрочитанные доступные сообщения по комнатам.</summary>
  public Dictionary<Guid, int> Unread { get; init; } = [];
}

/// <summary>Серверные операции чата, каждая повторно проверяет актуальный Identity и область доступа.</summary>
public interface IChatService
{
  Task<(Guid Room, int Take)> Locate(ClaimsPrincipal user, Guid message);
  Task<Guid> Main(ClaimsPrincipal user);
  Task<ChatSnapshot> Snapshot(ClaimsPrincipal user, Guid room, int take, bool archive);
  Task<List<ChatMessage>> Search(ClaimsPrincipal user, string query);
  Task<Guid> CreateRoom(ClaimsPrincipal user, string name);
  Task<Guid> Thread(ClaimsPrincipal user, Guid message);
  Task<Guid> Send(ClaimsPrincipal user, Guid room, string text, Guid[] readers, DateTime? confirmedUtc = null, string? zone = null);
  Task Edit(ClaimsPrincipal user, Guid message, string text);
  Task Delete(ClaimsPrincipal user, Guid message);
  Task Highlight(ClaimsPrincipal user, Guid message);
  Task Read(ClaimsPrincipal user, Guid room, Guid[] messages);
  Task Presence(ClaimsPrincipal user, Guid room, string connection, bool typing);
  Task<ChatPreference> Preference(ClaimsPrincipal user);
  Task SavePreference(ClaimsPrincipal user, ChatPreference input);
  Task CreatePoll(ClaimsPrincipal user, Guid room, string question, string[] options, bool multiple, DateTime endsUtc, Guid[] readers);
  Task AddOption(ClaimsPrincipal user, Guid poll, string text);
  Task<int> OptionVotes(ClaimsPrincipal user, Guid option);
  Task RemoveOption(ClaimsPrincipal user, Guid option, int confirmedVotes);
  Task Vote(ClaimsPrincipal user, Guid poll, Guid[] options);
  Task<ChatAttachment> Upload(ClaimsPrincipal user, Guid message, string name, Stream source, CancellationToken ct);
  Task<(Stream Content, string Name)> Download(ClaimsPrincipal user, Guid attachment, CancellationToken ct);
  Task<List<ChatNotice>> Notices(ClaimsPrincipal user);
  Task MarkNotice(ClaimsPrincipal user, Guid notice);
}
