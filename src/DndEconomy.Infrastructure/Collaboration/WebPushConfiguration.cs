using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace DndEconomy.Infrastructure.Collaboration;

/// <summary>Проверка полной VAPID-пары без раскрытия значений в диагностике.</summary>
public static class WebPushConfiguration
{
  /// <summary>Возвращает безопасное описание ошибки или null для готовой конфигурации.</summary>
  public static string? Error(IConfiguration config)
  {
    var missing = new[] { "Subject", "PublicKey", "PrivateKey" }
      .Where(name => string.IsNullOrWhiteSpace(config[$"WebPush:{name}"])).ToArray();
    if (missing.Length > 0) return "Web Push не настроен: отсутствуют " +
      string.Join(", ", missing.Select(name => "WebPush:" + name)) + ". Обратитесь к администратору.";
    if (!Uri.TryCreate(config["WebPush:Subject"], UriKind.Absolute, out var subject) ||
        !(subject.Scheme == "https" && !string.IsNullOrWhiteSpace(subject.Host) && subject.UserInfo.Length == 0 ||
          subject.Scheme == "mailto" && System.Net.Mail.MailAddress.TryCreate(config["WebPush:Subject"]![7..], out _)))
      return "Web Push не настроен: WebPush:Subject должен содержать mailto-контакт или HTTPS-адрес.";
    try
    {
      var publicKey = Decode(config["WebPush:PublicKey"]!);
      var privateKey = Decode(config["WebPush:PrivateKey"]!);
      if (publicKey.Length != 65 || publicKey[0] != 4 || privateKey.Length != 32)
        return "Web Push не настроен: некорректный формат VAPID-ключей.";
      // Производный публичный ключ обязан совпадать с переданным браузеру.
      using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = privateKey });
      var derived = key.ExportParameters(false);
      if (!CryptographicOperations.FixedTimeEquals(publicKey.AsSpan(1, 32), derived.Q.X!) ||
          !CryptographicOperations.FixedTimeEquals(publicKey.AsSpan(33, 32), derived.Q.Y!))
        return "Web Push не настроен: публичный и приватный VAPID-ключи не составляют пару.";
      return null;
    }
    catch (Exception e) when (e is FormatException or CryptographicException or ArgumentException)
    {
      return "Web Push не настроен: некорректный формат VAPID-ключей.";
    }
  }

  private static byte[] Decode(string value) => Convert.FromBase64String(
    value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
}
