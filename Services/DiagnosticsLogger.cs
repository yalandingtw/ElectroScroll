using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using ElectroScroll.Models;

namespace ElectroScroll.Services;

public sealed class DiagnosticsLogger : IDisposable
{
    private const int FlushIntervalMs = 500;
    private const int MaxQueuedLines = 2000;
    private readonly AppSettings _settings;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly System.Threading.Timer _timer;
    private int _isFlushing;
    private int _queuedLines;

    public DiagnosticsLogger(AppSettings settings)
    {
        _settings = settings;
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElectroScroll",
            "logs");

        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, "electroscroll.log");
        _timer = new System.Threading.Timer(Flush, null, FlushIntervalMs, FlushIntervalMs);
    }

    public string LogPath { get; }

    public bool Enabled => _settings.DiagnosticsLoggingEnabled;

    public void Log(string eventName, string details)
    {
        if (!Enabled)
        {
            return;
        }

        if (Interlocked.Increment(ref _queuedLines) > MaxQueuedLines)
        {
            Interlocked.Decrement(ref _queuedLines);
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:O}\t{Clean(eventName)}\t{Clean(details)}");
        _queue.Enqueue(line);
    }

    public void Dispose()
    {
        _timer.Dispose();
        Flush(null);
    }

    private void Flush(object? state)
    {
        if (_queue.IsEmpty || Interlocked.Exchange(ref _isFlushing, 1) == 1)
        {
            return;
        }

        try
        {
            RotateIfNeeded();

            using var writer = new StreamWriter(LogPath, append: true);
            while (_queue.TryDequeue(out var line))
            {
                Interlocked.Decrement(ref _queuedLines);
                writer.WriteLine(line);
            }
        }
        catch
        {
            while (_queue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queuedLines);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }

    private void RotateIfNeeded()
    {
        var maxBytes = Math.Max(64_000, _settings.DiagnosticsLogMaxBytes);
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length <= maxBytes)
        {
            return;
        }

        var archivePath = Path.Combine(Path.GetDirectoryName(LogPath)!, "electroscroll.1.log");
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(LogPath, archivePath);
    }

    private static string Clean(string value)
    {
        return value
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
