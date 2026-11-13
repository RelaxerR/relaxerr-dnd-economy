using System.Security.Claims;
using System.Net;
using System.Security.Cryptography;
using WebPush;
using System.Text.Json;
using DndEconomy.Application.Collaboration;
using DndEconomy.Application.Catalog;
using DndEconomy.Application.Pricing;
using DndEconomy.Infrastructure.Catalog;
using DndEconomy.Application.Economy;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Collaboration;
using DndEconomy.Infrastructure.Economy;
using DndEconomy.Infrastructure.Import;
using DndEconomy.Infrastructure.Activities;
using DndEconomy.Infrastructure.Persistence;
using DndEconomy.Infrastructure.Pricing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace DndEconomy.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
  public PostgresFactAttribute() { if (Environment.GetEnvironmentVariable("DND_TEST_POSTGRES") is null) Skip = "Set DND_TEST_POSTGRES to a disposable PostgreSQL server connection (CREATEDB required)."; }
}

public sealed class PostgresTests
{
  [PostgresFact]
  public async Task MigrationPrivacyFilesPollsSchedulingEconomyAndRestart()
  {
    var connection = Environment.GetEnvironmentVariable("DND_TEST_POSTGRES")!;
    var name = "relaxerr_chat_test_" + Guid.NewGuid().ToString("N");
    var root = Path.Combine(Path.GetTempPath(), name);
    await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
    await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
    var cs = new NpgsqlConnectionStringBuilder(connection) { Database = name }.ConnectionString;
    var factory = new TestFactory(cs);
    var clock = new TestClock(new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero));
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Chat:StoragePath"] = root }).Build();
    try
    {
      Guid manual;
      await using (var db = factory.CreateDbContext())
      {
        await db.GetService<IMigrator>().MigrateAsync("20260923172840_AddPartySizeCoefficients");
        var city = new City { Name = "Test City" }; db.Add(city);
        var session = new EconomySession { Name = "Manual", RealDate = new(2026, 11, 30), CityId = city.Id, Season = Season.Winter };
        await db.SaveChangesAsync(); manual = session.Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"EconomySessions\" (\"Id\",\"Name\",\"RealDate\",\"GameDateLabel\",\"CityId\",\"Season\",\"BaseCoefficient\",\"SellCoefficient\",\"IsPinnedForDisplay\",\"CreatedAtUtc\") VALUES ({session.Id},{session.Name},{session.RealDate},{session.GameDateLabel},{session.CityId},{(int)session.Season},{session.BaseCoefficient},{session.SellCoefficient},false,{session.CreatedAtUtc})");
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        Assert.False((await db.EconomySessions.SingleAsync()).IsCancelled);
      }
      var accounts = Enumerable.Range(0, 5).Select(i => new ApplicationUser { Id = Guid.NewGuid(), UserName = $"p{i}@test.invalid", Email = $"p{i}@test.invalid", DisplayName = $"P{i}", MustChangePassword = false, SecurityStamp = Guid.NewGuid().ToString() }).ToArray();
      var principals = accounts.Select((u, i) => new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim("AspNet.Identity.SecurityStamp", u.SecurityStamp!), new Claim(ClaimTypes.Role, i == 0 ? "Admin" : "Player") }, "test"))).ToArray();
      await using (var db = factory.CreateDbContext())
      {
        var master = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
        var player = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Player", NormalizedName = "PLAYER" };
        db.AddRange(master, player); db.AddRange(accounts);
        for (var i = 0; i < accounts.Length; i++) db.UserRoles.Add(new() { UserId = accounts[i].Id, RoleId = i == 0 ? master.Id : player.Id });
        await db.SaveChangesAsync();
      }
      var chat = new ChatService(factory, config, clock);
      var scheduling = new SchedulingService(factory, clock);
      var room = await chat.Main(principals[0]); Assert.Equal(room, await chat.Main(principals[1]));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.CreateRoom(principals[1], "Denied"));
      var dm = await chat.Send(principals[1], room, "private needle", [accounts[2].Id]);
      Assert.Contains(await chat.Notices(principals[2]), n => n.MessageId == dm);
      Assert.DoesNotContain(await chat.Notices(principals[3]), n => n.MessageId == dm);
      Assert.DoesNotContain(await chat.Notices(principals[0]), n => n.MessageId == dm);
      var recipientBeforeRead = await chat.Snapshot(principals[2], room, 50, false);
      Assert.Equal(1, recipientBeforeRead.Unread[room]);
      Assert.False((await chat.Snapshot(principals[3], room, 50, false)).Unread.ContainsKey(room));
      Assert.False((await chat.Snapshot(principals[1], room, 50, false)).Unread.ContainsKey(room));
      await chat.Read(principals[2], room, [dm]);
      Assert.False((await chat.Snapshot(principals[2], room, 50, false)).Unread.ContainsKey(room));
      var thread = await chat.Thread(principals[2], dm);
      await chat.Send(principals[2], thread, "private reply", []);
      var outsider = await chat.Snapshot(principals[3], room, 50, false);
      Assert.DoesNotContain(outsider.Messages, x => x.Message.Id == dm);
      Assert.DoesNotContain(outsider.Rooms, x => x.Id == thread);
      Assert.Empty(await chat.Search(principals[3], "private"));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Snapshot(principals[3], thread, 50, false));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Thread(principals[3], dm));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Locate(principals[3], dm));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Presence(principals[3], thread, "forbidden", true));
      await chat.Read(principals[3], room, [dm]);
      Assert.DoesNotContain((await chat.Snapshot(principals[1], room, 50, false)).Messages.Single(x => x.Message.Id == dm).ReadBy, x => x == accounts[3].Id);
      Assert.Contains((await chat.Snapshot(principals[0], room, 50, false)).Messages, x => x.Message.Id == dm);
      var file = await chat.Upload(principals[1], dm, "../../secret.txt", new MemoryStream([1, 2, 3]), default);
      Assert.Equal("secret.txt", file.Name);
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Download(principals[3], file.Id, default));
      var download = await chat.Download(principals[2], file.Id, default); await download.Content.DisposeAsync();
      var expiringMessage = await chat.Send(principals[1], room, "Expiring attachment", []);
      var boundary = await chat.Upload(principals[1], expiringMessage, "boundary.bin", new ZeroStream(100_000_000), default);
      Assert.Equal(100_000_000, boundary.Length);
      await Assert.ThrowsAsync<ArgumentException>(() => chat.Upload(principals[1], dm, "large.bin", new ZeroStream(100_000_001), default));
      await chat.CreatePoll(principals[1], room, "Question", ["One", "Two"], false, clock.GetUtcNow().UtcDateTime.AddHours(1), [accounts[2].Id]);
      var pv = (await chat.Snapshot(principals[1], room, 50, false)).Messages.Single(x => x.Poll is not null);
      await Task.WhenAll(chat.Vote(principals[1], pv.Poll!.Id, [pv.Options[0].Id]), chat.Vote(principals[2], pv.Poll.Id, [pv.Options[1].Id]));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Vote(principals[3], pv.Poll.Id, []));
      await chat.AddOption(principals[2], pv.Poll.Id, "Three");
      await Assert.ThrowsAsync<ArgumentException>(() => chat.RemoveOption(principals[1], pv.Options[0].Id, 0));
      await chat.RemoveOption(principals[1], pv.Options[0].Id, 1);
      Assert.Single((await chat.Snapshot(principals[1], room, 50, false)).Messages.Single(x => x.Poll is not null).Votes);
      var tomorrow = clock.GetUtcNow().UtcDateTime.Date.AddDays(1);
      for (var i = 0; i < 4; i++) await scheduling.SaveRule(principals[i], new() { Zone = "UTC", LocalStart = tomorrow.AddHours(19), LocalEnd = tomorrow.AddHours(23) });
      var requestId = await scheduling.Request(principals[0], room, "UTC", new());
      var snapshot = await chat.Snapshot(principals[0], room, 100, false);
      var request = snapshot.Requests.Single(); Assert.Equal("voting", request.State);
      var options = JsonSerializer.Deserialize<List<SessionWindow>>(request.OptionsJson)!;
      var winner = options[0];
      for (var i = 0; i < 4; i++) await scheduling.Ballot(principals[i], requestId, request.Round, [winner.Key]);
      Assert.Empty((await chat.Snapshot(principals[0], room, 100, false)).Games);
      await scheduling.Ballot(principals[4], requestId, request.Round, []);
      var assigned = (await chat.Snapshot(principals[0], room, 100, false)).Games.Single();
      Assert.Equal(winner.StartUtc, assigned.StartUtc);
      await Assert.ThrowsAsync<ArgumentException>(() => scheduling.Ballot(principals[1], requestId, request.Round, [winner.Key]));
      Guid cityId;
      await using (var db = factory.CreateDbContext()) cityId = await db.Cities.Select(x => x.Id).SingleAsync();
      var input = new NewEconomySessionInput { Name = "Linked", GameDateLabel = "Game date", RealDate = new(2020, 1, 1), CityId = cityId, Season = Season.Winter, BaseCoefficient = 2, SellCoefficient = .9m };
      var created = await Task.WhenAll(scheduling.CreateEconomy(principals[0], assigned.Id, input), scheduling.CreateEconomy(principals[0], assigned.Id, input));
      Assert.Equal(created[0], created[1]);
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scheduling.CreateEconomy(principals[1], assigned.Id, input));
      var item = new Item { NameRu = "Тестовый меч", Type = "Оружие", Subtype = "Меч", Category = "Предметы", BaseCost = 100 };
      await using (var db = factory.CreateDbContext()) { db.Add(item); await db.SaveChangesAsync(); }
      var priceStore = new EconomyPricingReadStore(factory, NullLogger<EconomyPricingReadStore>.Instance);
      var prices = new PriceCalculationService(priceStore, clock, NullLogger<PriceCalculationService>.Instance);
      var catalog = new CatalogQueryService(new CatalogReadStore(factory), priceStore, new CityCoinAcceptanceReadStore(factory), clock, NullLogger<CatalogQueryService>.Instance);
      var economy = new EconomyAdminService(factory);
      await economy.SetDisplaySessionOverrideAsync(created[0], default);
      Assert.Equal("Linked", (await new EconomyPricingReadStore(factory, NullLogger<EconomyPricingReadStore>.Instance).GetActiveSessionContextAsync(new(2026, 12, 2), default))!.SessionName);
      Assert.Equal(200m, (await prices.CalculateCurrentPriceAsync(item.Id, default))!.BuyPrice);
      Assert.Equal(200m, (await catalog.GetPageAsync(new(), default)).Items.Single().BuyPrice);
      var playerRule = (await scheduling.Calendar(principals[3])).Single();
      await scheduling.DeleteRule(principals[3], playerRule.Id);
      snapshot = await chat.Snapshot(principals[0], room, 100, false);
      Assert.NotNull(snapshot.Games.Single().CancelledUtc);
      Assert.Equal("waiting", snapshot.Requests.Single().State);
      await economy.SetDisplaySessionOverrideAsync(created[0], default);
      Assert.Equal("Manual", (await new EconomyPricingReadStore(factory, NullLogger<EconomyPricingReadStore>.Instance).GetActiveSessionContextAsync(new(2026, 12, 2), default))!.SessionName);
      await using (var db = factory.CreateDbContext())
      {
        Assert.True((await db.EconomySessions.SingleAsync(x => x.Id == created[0])).IsCancelled);
        Assert.False((await db.EconomySessions.SingleAsync(x => x.Id == created[0])).IsPinnedForDisplay);
        Assert.Equal(2, await db.EconomySessions.CountAsync());
      }
      Assert.Equal(100m, (await prices.CalculateCurrentPriceAsync(item.Id, default))!.BuyPrice);
      Assert.Equal(100m, (await catalog.GetPageAsync(new(), default)).Items.Single().BuyPrice);
      var exported = await new ExcelEconomyExportService(economy, new EconomyActivityAdminService(factory), new PartySizeCoefficientAdminService(factory)).ExportSessionsAsync(default);
      var importer = new ExcelEconomyImportService(factory, NullLogger<ExcelEconomyImportService>.Instance);
      var dryRun = await importer.ImportSessionsAsync(new MemoryStream(exported), true, false, default);
      Assert.Equal(0, dryRun.SessionsRemoved);
      var imported = await importer.ImportSessionsAsync(new MemoryStream(exported), true, true, default);
      Assert.Equal(2, imported.SessionsImported);
      await using (var db = factory.CreateDbContext()) Assert.True((await db.EconomySessions.SingleAsync(x => x.Id == created[0])).IsCancelled);
      await scheduling.SaveRule(principals[3], playerRule);
      var second = (await chat.Snapshot(principals[0], room, 100, false)).Requests.Single();
      Assert.Equal(request.Round + 1, second.Round);
      var secondOptions = JsonSerializer.Deserialize<List<SessionWindow>>(second.OptionsJson)!;
      var early = secondOptions[0]; var later = secondOptions[^1];
      await Task.WhenAll(Enumerable.Range(0, 5).Select(i => scheduling.Ballot(principals[i], requestId, second.Round,
        i == 4 ? [] : [i is 0 or 2 ? later.Key : early.Key])));
      var secondGame = (await chat.Snapshot(principals[0], room, 100, false)).Games.Single(x => x.CancelledUtc == null);
      Assert.Equal(later.StartUtc, secondGame.StartUtc); Assert.Equal(later.Format, secondGame.Format);
      await scheduling.DeleteRule(principals[3], playerRule.Id);
      await scheduling.SaveRule(principals[3], playerRule);
      var third = (await chat.Snapshot(principals[0], room, 100, false)).Requests.Single();
      await Task.WhenAll(principals.Select(p => scheduling.Ballot(p, requestId, third.Round, [])));
      Assert.Equal("no-votes", (await chat.Snapshot(principals[0], room, 100, false)).Requests.Single().State);
      Assert.Equal(2, (await chat.Snapshot(principals[0], room, 100, false)).Games.Count);

      // Real encryption and VAPID signing, fake provider: no requests leave the test process.
      var vapid = VapidHelper.GenerateVapidKeys();
      var pushConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["WebPush:PublicKey"] = vapid.PublicKey, ["WebPush:PrivateKey"] = vapid.PrivateKey, ["WebPush:Subject"] = "mailto:test@example.com" }).Build();
      var push = new PushService(factory, pushConfig);
      Assert.False(PushService.AllowedEndpoint("http://127.0.0.1/internal"));
      Assert.False(PushService.AllowedEndpoint("https://fcm.googleapis.com.attacker.invalid/send"));
      var endpoint = "https://fcm.googleapis.com/fcm/send/test";
      await push.Subscribe(principals[2], endpoint, vapid.PublicKey, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+','-').Replace('/','_'));
      await using (var db = factory.CreateDbContext())
      {
        (await db.Set<ChatPushSubscription>().SingleAsync()).CreatedUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-1);
        db.Add(new ChatNotice { UserId = accounts[2].Id, RoomId = room, MessageId = dm, EventKey = "test-delivery", Kind = "private", Title = "Новое личное сообщение", CreatedUtc = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync();
      }
      var httpHandler = new FakePushHandler(); using var http = new HttpClient(httpHandler); using var client = new WebPushClient(http);
      var pushWorker = new ChatPushWorker(factory, pushConfig, clock, NullLogger<ChatPushWorker>.Instance);
      await pushWorker.Deliver(client, default); await pushWorker.Deliver(client, default);
      Assert.Equal(1, httpHandler.Requests);
      await using (var db = factory.CreateDbContext())
      {
        db.Add(new ChatNotice { UserId = accounts[2].Id, RoomId = room, MessageId = dm, EventKey = "test-gone", Kind = "private", Title = "Новое личное сообщение", CreatedUtc = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync();
      }
      httpHandler.Status = HttpStatusCode.Gone;
      await pushWorker.Deliver(client, default);
      await using (var db = factory.CreateDbContext()) Assert.Empty(await db.Set<ChatPushSubscription>().ToListAsync());

      await chat.Delete(principals[1], dm);
      Assert.Empty(await chat.Search(principals[2], "needle"));
      Assert.Contains((await chat.Snapshot(principals[2], thread, 50, false)).Messages, x => x.Message.Text == "private reply");
      // Reconnect re-reads edited/deleted state; pagination and pins include older history once each.
      var oldPin = await chat.Send(principals[1], room, "older pin", []);
      await chat.Highlight(principals[0], oldPin);
      var edited = await chat.Send(principals[1], room, "before disconnect", []);
      for (var i = 0; i < 55; i++) await chat.Send(principals[2], room, $"history {i}", []);
      var recent = await chat.Snapshot(principals[1], room, 50, false);
      Assert.Equal(50, recent.Messages.Count); Assert.Contains(recent.Pinned, x => x.Id == oldPin);
      await chat.Edit(principals[1], edited, "after reconnect");
      await chat.Delete(principals[1], oldPin);
      var resumed = await new ChatService(factory, config, clock).Snapshot(principals[1], room, 200, false);
      Assert.Contains(resumed.Messages, x => x.Message.Id == edited && x.Message.Text == "after reconnect");
      Assert.DoesNotContain(resumed.Messages, x => x.Message.Id == oldPin);
      Assert.Equal(resumed.Messages.Count, resumed.Messages.Select(x => x.Message.Id).Distinct().Count());
      Assert.Empty(resumed.Pinned);
      clock.Advance(boundary.ExpiresUtc - clock.GetUtcNow().UtcDateTime - TimeSpan.FromTicks(1));
      var lastMoment = await chat.Download(principals[1], boundary.Id, default); await lastMoment.Content.DisposeAsync();
      clock.Advance(TimeSpan.FromTicks(1));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Download(principals[1], boundary.Id, default));
      await Assert.ThrowsAsync<ArgumentException>(() => chat.AddOption(principals[2], pv.Poll.Id, "Late"));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Download(principals[2], file.Id, default));
      var worker = new CollaborationWorker(factory, config, clock, NullLogger<CollaborationWorker>.Instance);
      await worker.Tick(default); await worker.Tick(default);
      Assert.False(File.Exists(Path.Combine(root, boundary.Id.ToString("N"))));
      Assert.Contains((await chat.Snapshot(principals[2], thread, 50, true)).Rooms, x => x.Id == thread);
      await chat.Send(principals[2], thread, "restore", []);
      Assert.Contains((await new ChatService(factory, config, clock).Snapshot(principals[2], thread, 50, false)).Rooms, x => x.Id == thread);
      await using (var db = factory.CreateDbContext()) { var u = await db.Users.SingleAsync(x => x.Id == accounts[2].Id); u.SecurityStamp = "revoked"; await db.SaveChangesAsync(); }
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => chat.Snapshot(principals[2], thread, 50, false));
    }
    finally
    {
      NpgsqlConnection.ClearAllPools();
      await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
      if (Directory.Exists(root)) Directory.Delete(root, true);
    }
  }

  private sealed class FakePushHandler : HttpMessageHandler
  {
    public int Requests { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests++; Assert.Equal("fcm.googleapis.com", request.RequestUri!.Host);
      return Task.FromResult(new HttpResponseMessage(Status));
    }
  }

  private sealed class TestFactory(string connection) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
  }
  private sealed class TestClock(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan span) => now += span;
  }
  private sealed class ZeroStream(long length) : Stream
  {
    private long position;
    public override int Read(byte[] buffer, int offset, int count) { var n = (int)Math.Min(count, length - position); Array.Clear(buffer, offset, n); position += n; return n; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { var n = (int)Math.Min(buffer.Length, length - position); buffer.Span[..n].Clear(); position += n; return ValueTask.FromResult(n); }
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => length; public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
