using DndEconomy.Infrastructure.Collaboration;
using Microsoft.Extensions.Configuration;
using WebPush;
using Xunit;

namespace DndEconomy.Tests;

public sealed class WebPushConfigurationTests
{
  [Fact]
  public void OnlyACompleteMatchingPairAndContactEnablePush()
  {
    var keys = VapidHelper.GenerateVapidKeys();
    var values = new Dictionary<string, string?> { ["WebPush:PublicKey"] = keys.PublicKey };
    IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    Assert.Contains("WebPush:PrivateKey", WebPushConfiguration.Error(Config()));
    Assert.Contains("WebPush:Subject", WebPushConfiguration.Error(Config()));
    values["WebPush:PrivateKey"] = keys.PrivateKey;
    values["WebPush:Subject"] = "mailto:owner@example.com";
    Assert.Null(WebPushConfiguration.Error(Config()));
    values["WebPush:PrivateKey"] = VapidHelper.GenerateVapidKeys().PrivateKey;
    Assert.Contains("не составляют пару", WebPushConfiguration.Error(Config()));
    values["WebPush:PrivateKey"] = "secret-invalid-value";
    Assert.DoesNotContain("secret-invalid-value", WebPushConfiguration.Error(Config()));
    values["WebPush:PrivateKey"] = keys.PrivateKey;
    values["WebPush:Subject"] = "http://localhost";
    Assert.Contains("WebPush:Subject", WebPushConfiguration.Error(Config()));
  }

  [Theory]
  [InlineData("https://fcm.googleapis.com/send", true)]
  [InlineData("https://updates.push.services.mozilla.com/wpush/v2/test", true)]
  [InlineData("https://web.push.apple.com/test", true)]
  [InlineData("https://fcm.googleapis.com:444/send", false)]
  [InlineData("https://fcm.googleapis.com@evil.example/send", false)]
  [InlineData("https://fcm.googleapis.com/send#fragment", false)]
  [InlineData("http://fcm.googleapis.com/send", false)]
  [InlineData("file:///etc/passwd", false)]
  [InlineData("https://evilpush.apple.com/send", false)]
  public void EndpointMustBeASupportedHttpsProvider(string endpoint, bool allowed) =>
    Assert.Equal(allowed, PushService.AllowedEndpoint(endpoint));
}
