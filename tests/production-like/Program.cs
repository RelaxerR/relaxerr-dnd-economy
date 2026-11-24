using System.Security.Claims;
using System.Text.Json;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Infrastructure.Collaboration;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connection = config["ConnectionStrings:Default"]!;
if (config["DND_ACCEPTANCE_FIXTURE"] != "1" || new NpgsqlConnectionStringBuilder(connection).Database != "relaxerr_acceptance")
  throw new InvalidOperationException("Fixture only supports the isolated relaxerr_acceptance database with DND_ACCEPTANCE_FIXTURE=1.");
var factory = new FixtureFactory(connection);
await using var db = factory.CreateDbContext();
await db.Database.MigrateAsync();
var chat = new ChatService(factory, config, TimeProvider.System);
if (args.SingleOrDefault() == "expire")
{
  var files = await db.Set<ChatAttachment>().ToListAsync();
  foreach (var file in files) file.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
  await db.SaveChangesAsync();
  await new CollaborationWorker(factory, config, TimeProvider.System, NullLogger<CollaborationWorker>.Instance).Tick(default);
  if (await db.Set<ChatAttachment>().AsNoTracking().AnyAsync()) throw new Exception("Cleanup failed");
  Console.WriteLine("Expired attachments removed; messages retained.");
  return;
}
if (await db.Users.AnyAsync()) throw new InvalidOperationException("Use a fresh acceptance volume; fixture refuses to replace users.");
var adminRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "Admin") ?? new IdentityRole<Guid>("Admin") { NormalizedName = "ADMIN" };
var playerRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "Player") ?? new IdentityRole<Guid>("Player") { NormalizedName = "PLAYER" };
if (db.Entry(adminRole).State == EntityState.Detached) db.Add(adminRole);
if (db.Entry(playerRole).State == EntityState.Detached) db.Add(playerRole);
var users = Enumerable.Range(0, 5).Select(i => new ApplicationUser {
  UserName = $"p{i}@acceptance.invalid", Email = $"p{i}@acceptance.invalid", DisplayName = $"Приёмка {i}",
  NormalizedUserName = $"P{i}@ACCEPTANCE.INVALID", NormalizedEmail = $"P{i}@ACCEPTANCE.INVALID",
  SecurityStamp = Guid.NewGuid().ToString(), MustChangePassword = false, EmailConfirmed = true
}).ToArray();
foreach (var (user, i) in users.Select((u, i) => (u, i)))
{
  user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Acceptance123!Local");
  db.Add(user); db.UserRoles.Add(new() { UserId = user.Id, RoleId = i == 0 ? adminRole.Id : playerRole.Id });
}
await db.SaveChangesAsync();
ClaimsPrincipal Principal(int i) => new(new ClaimsIdentity([
  new Claim(ClaimTypes.NameIdentifier, users[i].Id.ToString()), new Claim("AspNet.Identity.SecurityStamp", users[i].SecurityStamp!),
  new Claim(ClaimTypes.Role, i == 0 ? "Admin" : "Player")], "fixture"));
var calendarDate = DateTime.UtcNow.Date.AddDays(1);
await chat.SavePreference(Principal(2), new ChatPreference { Zone = "Europe/Berlin" });
await new SchedulingService(factory, TimeProvider.System).SaveRule(Principal(2), new AvailabilityRule
{
  Zone = "Europe/Berlin", Repeat = RepeatKind.Daily, LocalStart = calendarDate.AddHours(19), LocalEnd = calendarDate.AddHours(21)
});
var room = await chat.Main(Principal(0));
await chat.Send(Principal(0), room, "Приёмка чата: это изолированная тестовая база.", []);
var message = await chat.Send(Principal(1), room, "Текст остаётся после удаления вложения", [users[2].Id]);
var file100 = await chat.Upload(Principal(1), message, "../../boundary100.bin", new ZeroStream(100_000_000), default);
try
{
  await chat.Upload(Principal(1), message, "oversize.bin", new ZeroStream(100_000_001), default);
  throw new Exception("Oversize upload accepted");
}
catch (ArgumentException) { }
Console.WriteLine(JsonSerializer.Serialize(new
{
  room, message, file = file100.Id, file100.Length, calendarDate = DateOnly.FromDateTime(calendarDate),
  calendarOwner = users[2].Id, emptyCalendarOwner = users[3].Id
}));

sealed class FixtureFactory(string connection) : IDbContextFactory<ApplicationDbContext>
{
  public ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
}

sealed class ZeroStream(long remaining) : Stream
{
  public override bool CanRead => true;
  public override bool CanSeek => false;
  public override bool CanWrite => false;
  public override long Length => throw new NotSupportedException();
  public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
  public override int Read(byte[] buffer, int offset, int count)
  {
    var n = (int)Math.Min(count, remaining); Array.Clear(buffer, offset, n); remaining -= n; return n;
  }
  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
  {
    ct.ThrowIfCancellationRequested(); var n = (int)Math.Min(buffer.Length, remaining); buffer.Span[..n].Clear(); remaining -= n; return ValueTask.FromResult(n);
  }
  public override void Flush() => throw new NotSupportedException();
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
  public override void SetLength(long value) => throw new NotSupportedException();
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
