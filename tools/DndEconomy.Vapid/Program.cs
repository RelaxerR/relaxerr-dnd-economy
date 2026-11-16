using System.Security.Cryptography;

// Never write private keys to stdout (including Actions output or terminal history).
if (args.Length != 4 || args[0] != "--output" || args[2] != "--subject" ||
    !Uri.TryCreate(args[3], UriKind.Absolute, out var subject) ||
    !(subject.Scheme == "mailto" && System.Net.Mail.MailAddress.TryCreate(args[3][7..], out _) || subject.Scheme == "https" && subject.Host.Length > 0) ||
    args[3].Any(char.IsWhiteSpace))
{
  Console.Error.WriteLine("Usage: dotnet run --project tools/DndEconomy.Vapid -- --output /private/path/web-push.env --subject mailto:owner@example.com");
  return 1;
}
using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var parameters = key.ExportParameters(true);
var publicBytes = new byte[65];
publicBytes[0] = 4;
parameters.Q.X!.CopyTo(publicBytes, 1);
parameters.Q.Y!.CopyTo(publicBytes, 33);
static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
try
{
  var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
  if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
  using var file = new FileStream(args[1], options);
  using var writer = new StreamWriter(file);
  writer.WriteLine($"WEB_PUSH_SUBJECT={args[3]}");
  writer.WriteLine($"WEB_PUSH_PUBLIC_KEY={Encode(publicBytes)}");
  writer.WriteLine($"WEB_PUSH_PRIVATE_KEY={Encode(parameters.D!)}");
  Console.WriteLine("VAPID configuration saved to a new private file. Existing files are never overwritten.");
  return 0;
}
catch (IOException)
{
  Console.Error.WriteLine("Cannot create the output file. Use a new path in a private, writable directory.");
  return 1;
}
