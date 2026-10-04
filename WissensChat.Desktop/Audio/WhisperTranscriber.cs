using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace WissensChat.Desktop.Audio;

/// Lokale Spracherkennung mit Whisper.net. Das Modell wird beim ersten Start automatisch geladen.
public sealed class WhisperTranscriber(AudioSettings cfg)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    public async Task<string> TranscribeAsync(string wavPath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_processor is null)
            {
                var modelPath = await EnsureModelAsync(progress, ct);
                progress?.Report("Whisper-Modell wird geladen …");
                _factory = await Task.Run(() => WhisperFactory.FromPath(modelPath), ct);
                _processor = _factory.CreateBuilder().WithLanguage(cfg.Language).Build();
            }

            progress?.Report("Sprache wird erkannt …");
            var sb = new StringBuilder();
            await using var fs = File.OpenRead(wavPath);
            await foreach (var segment in _processor.ProcessAsync(fs, ct))
                sb.Append(segment.Text);
            return sb.ToString().Trim();
        }
        finally { _gate.Release(); }
    }

    private async Task<string> EnsureModelAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var path = Path.Combine(cfg.ModelDirectory, $"ggml-{cfg.WhisperModel.ToLowerInvariant()}.bin");
        if (File.Exists(path)) return path;

        Directory.CreateDirectory(cfg.ModelDirectory);
        progress?.Report($"Whisper-Modell '{cfg.WhisperModel}' wird heruntergeladen (einmalig, kann dauern) …");
        var type = Enum.Parse<GgmlType>(cfg.WhisperModel, ignoreCase: true);
        var tmp = path + ".part";
        await using (var src = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type))
        await using (var dst = File.Create(tmp))
            await src.CopyToAsync(dst, ct);
        File.Move(tmp, path, overwrite: true);
        return path;
    }
}
