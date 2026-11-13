using System.Security.Cryptography;

// Generate once on a trusted machine. Do not commit the output or include it in CI logs.
using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var parameters = key.ExportParameters(true);
var publicBytes = new byte[65];
publicBytes[0] = 4;
parameters.Q.X!.CopyTo(publicBytes, 1);
parameters.Q.Y!.CopyTo(publicBytes, 33);
static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
Console.WriteLine($"WEB_PUSH_PUBLIC_KEY={Encode(publicBytes)}");
Console.WriteLine($"WEB_PUSH_PRIVATE_KEY={Encode(parameters.D!)}");
