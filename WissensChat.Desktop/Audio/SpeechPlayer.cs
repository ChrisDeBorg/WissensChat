using System.Diagnostics;
using System.Globalization;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Channels;
using NAudio.Wave;

namespace WissensChat.Desktop.Audio;

/// Text-to-Speech: Piper (falls installiert) oder Windows-Stimme. Erzeugt eine WAV-Datei.
public sealed class TtsEngine(AudioSettings cfg)
{
    public string Name => cfg.PiperAvailable ? "Piper" : "Windows";

    public Task SynthesizeAsync(string text, string wavPath, CancellationToken ct) =>
        cfg.PiperAvailable ? PiperAsync(text, wavPath, ct) : Task.Run(() => WindowsSpeak(text, wavPath), ct);

    private async Task PiperAsync(string text, string wavPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(cfg.PiperExe)
        {
            WorkingDirectory = Path.GetDirectoryName(cfg.PiperExe)!,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false)
        };
        psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(cfg.PiperVoice);
        psi.ArgumentList.Add("--output_file"); psi.ArgumentList.Add(wavPath);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Piper konnte nicht gestartet werden.");
        var stderr = p.StandardError.ReadToEndAsync(ct);
        await p.StandardInput.WriteLineAsync(text.AsMemory(), ct);
        p.StandardInput.Close();
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new InvalidOperationException($"Piper-Fehler: {await stderr}");
    }

    private static void WindowsSpeak(string text, string wavPath)
    {
        using var synth = new SpeechSynthesizer();
        try { synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new CultureInfo("de-DE")); }
        catch { /* keine deutsche Stimme installiert -> Standardstimme */ }
        synth.SetOutputToWaveFile(wavPath);
        synth.Speak(text);
    }
}

/// Spielt Sätze nacheinander ab. Enqueue() kann schon während des Token-Streams aufgerufen werden.
public sealed class SpeechPlayer(TtsEngine tts)
{
    private readonly object _lock = new();
    private Channel<string>? _queue;
    private CancellationTokenSource? _cts;

    public event Action<string>? Error;

    public void Enqueue(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return;
        lock (_lock)
        {
            if (_queue is null)
            {
                _queue = Channel.CreateUnbounded<string>();
                _cts = new CancellationTokenSource();
                var (q, token) = (_queue, _cts.Token);
                _ = Task.Run(() => RunAsync(q, token));
            }
            _queue.Writer.TryWrite(sentence);
        }
    }

    /// Bricht Wiedergabe und Warteschlange sofort ab.
    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _queue?.Writer.TryComplete();
            _queue = null; _cts = null;
        }
    }

    private async Task RunAsync(Channel<string> queue, CancellationToken ct)
    {
        try
        {
            await foreach (var sentence in queue.Reader.ReadAllAsync(ct))
            {
                var wav = Path.Combine(Path.GetTempPath(), $"wc_tts_{Guid.NewGuid():N}.wav");
                try
                {
                    await tts.SynthesizeAsync(sentence, wav, ct);
                    await PlayAsync(wav, ct);
                }
                finally { try { File.Delete(wav); } catch { } }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error?.Invoke($"Sprachausgabe: {ex.Message}"); }
    }

    private static async Task PlayAsync(string wav, CancellationToken ct)
    {
        using var reader = new AudioFileReader(wav);
        using var output = new WaveOutEvent();
        var done = new TaskCompletionSource();
        output.PlaybackStopped += (_, _) => done.TrySetResult();
        output.Init(reader);
        output.Play();
        using (ct.Register(() => output.Stop()))
            await done.Task;
    }
}
