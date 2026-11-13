using System.Security.Claims;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Collaboration;

public sealed partial class ChatService
{
  /// <inheritdoc />
  public async Task<ChatAttachment> Upload(ClaimsPrincipal user, Guid message, string name, Stream source, CancellationToken ct)
  {
    // Проверка до приёма байтов и после него: права/сообщение могли измениться за время загрузки.
    await using (var db = await factory.CreateDbContextAsync(ct))
    {
      var a = await ChatAccess.Authenticate(db, user);
      var m = await ChatAccess.Message(db, a, message);
      if (m.AuthorId != a.Id) throw new UnauthorizedAccessException();
    }
    name = Path.GetFileName(name.Replace('\\', '/'));
    name = new string(name.Where(x => !char.IsControl(x)).Take(255).ToArray());
    if (string.IsNullOrWhiteSpace(name)) name = "file";
    Directory.CreateDirectory(Root);
    var file = new ChatAttachment { MessageId = message, Name = name, ExpiresUtc = Now.AddDays(7) };
    var path = Path.Combine(Root, file.Id.ToString("N"));
    try
    {
      await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
      {
        var buffer = new byte[65536];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
          file.Length += read;
          if (file.Length > MaxFileBytes) throw new ArgumentException("Лимит файла — 100 000 000 байт.");
          await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
      }
      return await Write(user, async (db, a) =>
      {
        var m = await ChatAccess.Message(db, a, message);
        if (m.AuthorId != a.Id) throw new UnauthorizedAccessException();
        file.ExpiresUtc = Now.AddDays(7); db.Add(file); return file;
      });
    }
    catch { File.Delete(path); throw; }
  }

  /// <inheritdoc />
  public async Task<(Stream Content, string Name)> Download(ClaimsPrincipal user, Guid attachment, CancellationToken ct)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var a = await ChatAccess.Authenticate(db, user);
    var file = await db.Set<ChatAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachment, ct) ?? throw new UnauthorizedAccessException();
    await ChatAccess.Message(db, a, file.MessageId);
    if (file.ExpiresUtc <= Now) throw new UnauthorizedAccessException();
    var path = Path.Combine(Root, file.Id.ToString("N"));
    try { return (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true), file.Name); }
    catch (FileNotFoundException) { throw new UnauthorizedAccessException(); }
  }

  /// <inheritdoc />
  public async Task<List<ChatNotice>> Notices(ClaimsPrincipal user)
  {
    await using var db = await factory.CreateDbContextAsync(); var a = await ChatAccess.Authenticate(db, user);
    return await db.Set<ChatNotice>().AsNoTracking().Where(x => x.UserId == a.Id &&
      ChatAccess.Rooms(db, a).Any(r => r.Id == x.RoomId) &&
      (x.MessageId == null || ChatAccess.Messages(db, a).Any(m => m.Id == x.MessageId)))
      .OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync();
  }

  /// <inheritdoc />
  public Task MarkNotice(ClaimsPrincipal user, Guid notice) => Write(user, async (db, a) =>
  {
    var n = await db.Set<ChatNotice>().SingleOrDefaultAsync(x => x.Id == notice && x.UserId == a.Id) ?? throw new UnauthorizedAccessException();
    await ChatAccess.Room(db, a, n.RoomId);
    if (n.MessageId is { } id) await ChatAccess.Message(db, a, id);
    n.Read = true; return true;
  });
}
