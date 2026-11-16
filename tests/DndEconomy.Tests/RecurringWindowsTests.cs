using System.Net;
using DndEconomy.Domain.Collaboration;
using DndEconomy.Web.Components.Pages.Chat;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace DndEconomy.Tests;

public sealed class RecurringWindowsTests
{
  [Fact]
  public async Task ListIncludesExpiredCoveredAndMalformedRulesWithoutExpandingCalendar()
  {
    var expired = new AvailabilityRule { Repeat = RepeatKind.Daily, Zone = "Europe/Berlin",
      LocalStart = new(2020, 1, 1, 23, 0, 0), LocalEnd = new(2020, 1, 2, 2, 0, 0), RepeatUntil = new(2020, 1, 2) };
    var covered = new AvailabilityRule { Repeat = RepeatKind.Monthly, Days = [31], Zone = "UTC",
      LocalStart = new(2026, 1, 1, 19, 0, 0), LocalEnd = new(2026, 1, 1, 21, 0, 0), Status = AvailabilityStatus.Unavailable, Format = PlayFormat.Offline };
    var malformed = new AvailabilityRule { Repeat = RepeatKind.Weekly, Days = [], Zone = "Unknown/Zone",
      LocalStart = new(2026, 1, 1, 7, 45, 0), LocalEnd = new(2026, 1, 1, 16, 30, 0) };
    var once = new AvailabilityRule { LocalStart = new(2026, 1, 31), LocalEnd = new(2026, 2, 1), Zone = "UTC" };
    var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, NoJS>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var output = await renderer.RenderComponentAsync<RecurringWindows>(ParameterView.FromDictionary(new Dictionary<string, object?>
        { [nameof(RecurringWindows.Rules)] = new List<AvailabilityRule> { expired, covered, malformed, once } }));
      return WebUtility.HtmlDecode(output.ToHtmlString());
    });
    Assert.Contains("Повторяющиеся окна", html);
    Assert.Contains("02.01.2020", html); Assert.Contains("до следующего дня", html);
    Assert.Contains("По числам месяца", html); Assert.Contains("31", html);
    Assert.Contains("Точно не смогу", html); Assert.Contains("Офлайн", html);
    Assert.Contains("без даты окончания", html); Assert.Contains("Unknown/Zone", html);
    Assert.Contains("Параметры не создают дат", html); Assert.Contains("07:45–16:30", html);
    Assert.Equal(3, html.Split(">Изменить</button>").Length - 1);
    Assert.Contains("Удалить разовое окно", html);
  }

  private sealed class NoJS : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("SSR must not call JS.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
  }
}
