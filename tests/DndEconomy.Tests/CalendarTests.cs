using DndEconomy.Application.Collaboration;
using DndEconomy.Domain.Collaboration;
using Xunit;

namespace DndEconomy.Tests;

public sealed class CalendarTests
{
  private static readonly DateTime Start = new(2026, 12, 1, 19, 0, 0, DateTimeKind.Utc);
  private static readonly Guid Master = Guid.NewGuid();
  private static readonly Guid[] Players = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
  private static List<AvailabilitySlice> Slices(params (int Late, AvailabilityStatus Status)[] players)
  {
    var list = new List<AvailabilitySlice> { new(Master, Start, Start.AddHours(4), AvailabilityStatus.Certain, PlayFormat.Online) };
    list.AddRange(players.Select((p, i) => new AvailabilitySlice(Players[i], Start.AddMinutes(p.Late), Start.AddHours(4), p.Status, PlayFormat.Online)));
    return list;
  }
  private static SessionWindow? Evaluate(List<AvailabilitySlice> slices, int count) => SessionPlanner.Evaluate(Master, Players.Take(count).ToArray(), slices, Start, Start.AddHours(4), PlayFormat.Online, new());

  [Theory]
  [InlineData(60, true)] [InlineData(61, false)]
  public void FourAllowsOneLateUpToHour(int late, bool expected) => Assert.Equal(expected, Evaluate(Slices((0, 0), (0, 0), (late, 0)), 3)!.Color < 3);

  [Theory]
  [InlineData(120, 4)] [InlineData(121, 3)]
  public void FiveAllowsOneLateUpToTwoHours(int late, int count) => Assert.Equal(count, Evaluate(Slices((0, 0), (0, 0), (0, 0), (late, 0)), 4)!.Players.Count);

  [Fact]
  public void SixAllowsTwoLateAndChoosesValidSubset()
  {
    var w = Evaluate(Slices((0, 0), (0, 0), (0, 0), (120, 0), (120, 0), (120, 0)), 6)!;
    Assert.Equal(5, w.Players.Count); Assert.Equal(2, w.Players.Count(x => x.AtUtc > Start)); Assert.Single(w.Alternatives);
  }
  [Fact]
  public void ConditionalExtraDoesNotChangeConfirmedColor()
  {
    var w = Evaluate(Slices((0, 0), (0, 0), (0, 0), (0, AvailabilityStatus.Unlikely)), 4)!;
    Assert.Equal(0, w.Color); Assert.Equal(3, w.Players.Count); Assert.Single(w.Alternatives);
  }
  [Fact]
  public void ConditionalMinimumHasOrangeColor() => Assert.Equal(2, Evaluate(Slices((0, 0), (0, 0), (0, AvailabilityStatus.Unlikely)), 3)!.Color);
  [Fact]
  public void MasterCannotArriveLateOrHaveGap()
  {
    var s = Slices((0, 0), (0, 0), (0, 0));
    s[0] = s[0] with { StartUtc = Start.AddMinutes(1) }; Assert.Equal(3, Evaluate(s, 3)!.Color);
    s[0] = s[0] with { StartUtc = Start, EndUtc = Start.AddHours(2) }; Assert.Equal(3, Evaluate(s, 3)!.Color);
  }
  [Fact]
  public void AdjacentSlicesMergeWithWorstStatusAndFormatsStayIndependent()
  {
    var s = Slices((0, 0), (0, 0), (0, 0)); s.RemoveAt(1);
    s.Add(new(Players[0], Start, Start.AddHours(2), AvailabilityStatus.Certain, PlayFormat.Online));
    s.Add(new(Players[0], Start.AddHours(2), Start.AddHours(4), AvailabilityStatus.PreferOther, PlayFormat.Online));
    Assert.Equal(1, Evaluate(s, 3)!.Color);
    Assert.Equal(3, SessionPlanner.Evaluate(Master, Players.Take(3).ToArray(), s, Start, Start.AddHours(4), PlayFormat.Offline, new())!.Color);
  }
  [Fact]
  public void GapExcludesPlayerIfTailExceedsLatenessLimit()
  {
    var s = Slices((0, 0), (0, 0), (0, 0)); s.RemoveAt(3);
    s.Add(new(Players[2], Start, Start.AddMinutes(20), 0, PlayFormat.Online));
    s.Add(new(Players[2], Start.AddMinutes(61), Start.AddHours(4), 0, PlayFormat.Online)); Assert.Equal(3, Evaluate(s, 3)!.Color);
  }
  [Fact]
  public void GreenLaterStartRanksBeforeYellowEarlierStart()
  {
    var s = Slices((0, 0), (0, 0), (60, 0));
    var windows = SessionPlanner.Find(Master, Players.Take(3).ToArray(), s, Start, Start.AddHours(4), new());
    Assert.Equal(Start.AddHours(1), windows[0].StartUtc); Assert.Equal(0, windows[0].Color);
    Assert.Contains(windows, w => w.StartUtc == Start && w.Color == 1);
  }
  [Fact]
  public void AmbiguousAndMissingDstTimeRequireCorrection()
  {
    Assert.Throws<ArgumentException>(() => CalendarEngine.ToUtc(new(2026, 3, 29, 2, 30, 0), "Europe/Berlin"));
    Assert.Throws<ArgumentException>(() => CalendarEngine.ToUtc(new(2026, 10, 25, 2, 30, 0), "Europe/Berlin"));
    Assert.Equal(new DateTime(2026, 12, 1, 23, 0, 0, DateTimeKind.Utc), CalendarEngine.ToUtc(new(2026, 12, 2, 3, 0, 0), "Europe/Samara"));
  }
  [Fact]
  public void OnceOverridesRepeatIndependentlyForEachFormat()
  {
    var rules = new List<AvailabilityRule>
    {
      new() { UserId = Master, LocalStart = new(2026,12,1,0,0,0), LocalEnd = new(2026,12,31,0,0,0), Zone = "UTC", Repeat = RepeatKind.Daily },
      new() { UserId = Master, StartUtc = Start, EndUtc = Start.AddHours(1), Status = AvailabilityStatus.Unavailable, Format = PlayFormat.Online, UpdatedUtc = Start.AddDays(-2) }
    };
    var slices = CalendarEngine.Expand(rules, Start, Start.AddDays(1));
    Assert.Contains(slices, x => x.Format == PlayFormat.Online && x.Status == AvailabilityStatus.Unavailable && x.StartUtc == Start);
    Assert.DoesNotContain(slices, x => x.Format == PlayFormat.Offline && x.Status == AvailabilityStatus.Unavailable);
    Assert.Equal(48, slices.Sum(x => (x.EndUtc - x.StartUtc).TotalHours));
  }
  [Theory]
  [InlineData("Завтра в 19", false)] [InlineData("2026-12-02 19:00Z", false)]
  [InlineData("2026-12-02 19:00+04:00", false)] [InlineData("2026-12-02 19:00", true)]
  [InlineData("2026-12-02 19:00 и 2026-12-03 20:00", false)]
  public void ChatTimeRequiresOneFullUnambiguousTimestamp(string text, bool recognized) => Assert.Equal(recognized, ChatTimeParser.Recognize(text) is not null);

  [Fact]
  public void MonthlySkipsNonexistentDates()
  {
    var r = new AvailabilityRule { UserId = Master, Repeat = RepeatKind.Monthly, Days = [31], Zone = "UTC", LocalStart = new(2026,1,1,19,0,0), LocalEnd = new(2026,12,31,23,0,0) };
    var from = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    Assert.Empty(CalendarEngine.Expand([r], from, from.AddMonths(1)));
    Assert.Equal(8, CalendarEngine.Expand([r], from.AddMonths(1), from.AddMonths(2)).Sum(x => (x.EndUtc - x.StartUtc).TotalHours));
  }
}
