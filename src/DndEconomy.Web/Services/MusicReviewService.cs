using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DndEconomy.Web.Services;

/// <summary>Настройки каталогов для разбора музыки.</summary>
public sealed class MusicReviewOptions
{
  public string SourcePath { get; set; } = "";
  public string DestinationPath { get; set; } = "";
}

/// <summary>Трек в одном из музыкальных каталогов.</summary>
public sealed record MusicTrack(string Path, string Name, bool Reviewed);

/// <summary>Снимок каталогов и прогресса.</summary>
public sealed record MusicLibrary(IReadOnlyList<MusicTrack> Source, IReadOnlyList<MusicTrack> Destination,
  IReadOnlyList<string> Folders);

/// <summary>Серверная обработка, конвертация и размещение музыкальных файлов.</summary>
public sealed class MusicReviewService(IOptions<MusicReviewOptions> options, ILogger<MusicReviewService> logger)
{
  private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".ogg", ".opus", ".wav", ".flac", ".webm", ".m4a", ".aac", ".wma", ".aiff", ".aif" };
  private readonly SemaphoreSlim gate = new(1, 1);
  private readonly string source = Path.GetFullPath(options.Value.SourcePath);
  private readonly string destination = Path.GetFullPath(options.Value.DestinationPath);
  private readonly string previewRoot = Path.Combine(Path.GetTempPath(), "dnd-music-review");

  public string SourceRoot => source;
  public string DestinationRoot => destination;

  public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SourcePath)
    && !string.IsNullOrWhiteSpace(options.Value.DestinationPath);

  public MusicLibrary GetLibrary()
  {
    EnsureConfigured();
    var sourceTracks = Directory.Exists(source) ? EnumerateTracks(source, false) : [];
    var destinationTracks = Directory.Exists(Path.Combine(destination, "все"))
      ? EnumerateTracks(Path.Combine(destination, "все"), true) : [];
    var folders = Directory.Exists(destination)
      ? Directory.EnumerateDirectories(destination, "*", SearchOption.AllDirectories)
        .Where(p => !IsLink(p))
        .Select(p => Path.GetRelativePath(destination, p))
        .Where(p => p != "все" && !p.StartsWith($"все{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .Order(StringComparer.OrdinalIgnoreCase).ToArray()
      : [];
    return new MusicLibrary(sourceTracks, destinationTracks, folders);
  }

  public static string? ValidateName(string name)
  {
    if (string.IsNullOrWhiteSpace(name)) return "Введите имя трека.";
    if (name != name.Trim()) return "Уберите пробелы по краям.";
    if (name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
      return "Разрешены только латинские буквы, цифры, дефис и подчёркивание.";
    return null;
  }

  public static string? ValidateFolder(string folder)
  {
    if (string.IsNullOrWhiteSpace(folder)) return "Выберите папку.";
    if (folder.Split('/')[0] == "все") return "Папка «все» зарезервирована.";
    foreach (var part in folder.Split('/'))
      if (ValidateName(part) is { } error) return error;
    return null;
  }

  public string? CheckCollision(MusicTrack track, string name)
  {
    var error = ValidateName(name);
    if (error is not null) return error;
    var target = Path.Combine(destination, "все", name + ".opus");
    if (File.Exists(target) && !(track.Reviewed && Path.GetFullPath(track.Path) == target))
      return "Файл с таким именем уже есть в папке «все».";
    return null;
  }

  public async Task<string> PreparePreviewAsync(MusicTrack track, CancellationToken cancellationToken)
  {
    var path = ValidateTrack(track);
    if (track.Reviewed) return path;
    Directory.CreateDirectory(previewRoot);
    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path + File.GetLastWriteTimeUtc(path).Ticks)));
    var preview = Path.Combine(previewRoot, key + ".opus");
    if (File.Exists(preview)) return preview;
    var temp = preview + ".tmp";
    try
    {
      await RunFfmpegAsync(["-y", "-i", path, "-vn", "-map_metadata", "-1", "-c:a", "libopus", "-b:a", "128k", "-f", "opus", temp], cancellationToken);
      File.Move(temp, preview, true);
      return preview;
    }
    finally { if (File.Exists(temp)) File.Delete(temp); }
  }

  public async Task SaveAsync(MusicTrack track, string name, double start, double end,
    IReadOnlyList<string> folders, CancellationToken cancellationToken)
  {
    await gate.WaitAsync(cancellationToken);
    try
    {
      var path = ValidateTrack(track);
      if (CheckCollision(track, name) is { } error) throw new InvalidOperationException(error);
      var selected = folders.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
      if (selected.Length == 0) throw new InvalidOperationException("Выберите хотя бы одну папку.");
      foreach (var folder in selected)
        if (ValidateFolder(folder) is { } folderError) throw new InvalidOperationException(folderError);
      if (start < 0 || end <= start || !double.IsFinite(start) || !double.IsFinite(end))
        throw new InvalidOperationException("Проверьте границы обрезки.");
      var all = Path.Combine(destination, "все");
      Directory.CreateDirectory(all);
      var output = Path.Combine(all, name + ".opus");
      var trim = start > .01 || end < await GetDurationAsync(path, cancellationToken) - .01;
      var needsEncode = !track.Reviewed || trim;
      var staging = Path.Combine(all, ".music-review-" + Guid.NewGuid().ToString("N") + ".opus");
      var linkPaths = selected.Select(folder => Path.Combine(destination, folder.Replace('/', Path.DirectorySeparatorChar), name + ".opus")).ToArray();
      var newLinks = new List<string>();
      foreach (var link in linkPaths)
      {
        EnsureNoLinkedParent(Path.GetDirectoryName(link)!);
        if (File.Exists(link) || Directory.Exists(link))
        {
          if (track.Reviewed && IsLink(link) && new FileInfo(link).ResolveLinkTarget(true)?.FullName == path)
            continue;
          throw new InvalidOperationException($"Файл уже существует: {link}");
        }
        newLinks.Add(link);
      }
      var createdLinks = new List<string>();
      var oldLinks = track.Reviewed && path != output
        ? Directory.EnumerateFiles(destination, "*.opus", SearchOption.AllDirectories)
          .Where(p => IsLink(p) && new FileInfo(p).ResolveLinkTarget(true)?.FullName == path).ToArray()
        : [];
      try
      {
        if (needsEncode)
        {
          await RunFfmpegAsync(["-y", "-ss", start.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-i", path, "-t", (end - start).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-vn", "-map_metadata", "-1", "-c:a", "libopus", "-b:a", "128k", staging], cancellationToken);
        }
        foreach (var link in newLinks)
        {
          Directory.CreateDirectory(Path.GetDirectoryName(link)!);
          File.CreateSymbolicLink(link, Path.GetRelativePath(Path.GetDirectoryName(link)!, output));
          createdLinks.Add(link);
        }
        if (!track.Reviewed)
        {
          File.Move(staging, output);
          File.Delete(path);
        }
        else if (needsEncode || path != output)
        {
          if (needsEncode) File.Move(staging, output, true);
          else File.Move(path, output);
          foreach (var oldLink in oldLinks)
          {
            File.Delete(oldLink);
            File.CreateSymbolicLink(oldLink, Path.GetRelativePath(Path.GetDirectoryName(oldLink)!, output));
          }
          if (needsEncode && path != output) File.Delete(path);
        }
      }
      catch
      {
        foreach (var link in createdLinks) if (File.Exists(link)) File.Delete(link);
        throw;
      }
      finally { if (File.Exists(staging)) File.Delete(staging); }
    }
    finally { gate.Release(); }
  }

  public async Task DeleteSourceAsync(MusicTrack track, CancellationToken cancellationToken)
  {
    if (track.Reviewed) return;
    await gate.WaitAsync(cancellationToken);
    try { File.Delete(ValidateTrack(track)); }
    finally { gate.Release(); }
  }

  public string ValidateTrack(MusicTrack track)
  {
    EnsureConfigured();
    var root = track.Reviewed ? Path.Combine(destination, "все") : source;
    var path = Path.GetFullPath(track.Path);
    if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
      || !Extensions.Contains(Path.GetExtension(path)) || !File.Exists(path) || IsLink(path))
      throw new FileNotFoundException("Трек не найден в разрешённом каталоге.");
    return path;
  }

  private void EnsureNoLinkedParent(string path)
  {
    for (var current = path; current.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal);
      current = Path.GetDirectoryName(current)!)
      if (Directory.Exists(current) && IsLink(current))
        throw new InvalidOperationException("Ссылки на каталоги назначения не поддерживаются.");
  }

  private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

  private static MusicTrack[] EnumerateTracks(string root, bool reviewed) =>
    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
      .Where(p => Extensions.Contains(Path.GetExtension(p)) && !IsLink(p))
      .Select(p => new MusicTrack(p, Path.GetFileName(p), reviewed)).ToArray();

  private void EnsureConfigured()
  {
    if (!IsConfigured || source == destination || destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal)
      || source.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal))
      throw new InvalidOperationException("Настройте разные, не вложенные друг в друга пути MusicReview:SourcePath и DestinationPath.");
  }

  public static async Task<double> GetDurationAsync(string path, CancellationToken ct)
  {
    var psi = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path }) psi.ArgumentList.Add(arg);
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffprobe не запустился.");
    var output = await process.StandardOutput.ReadToEndAsync(ct);
    await process.WaitForExitAsync(ct);
    if (process.ExitCode != 0 || !double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var duration))
      throw new InvalidOperationException("Не удалось определить длительность трека.");
    return duration;
  }

  private async Task RunFfmpegAsync(string[] args, CancellationToken ct)
  {
    var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg не запустился.");
    var stderr = await process.StandardError.ReadToEndAsync(ct);
    await process.WaitForExitAsync(ct);
    if (process.ExitCode != 0)
    {
      logger.LogWarning("FFmpeg failed: {Error}", stderr);
      throw new InvalidOperationException("Не удалось конвертировать трек. Проверьте формат исходного файла и журнал сервера.");
    }
  }
}
