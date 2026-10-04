using NAudio;
using NAudio.Wave;

namespace WissensChat.Desktop.Audio;

/// Nimmt 16 kHz / 16 Bit / Mono auf (das Format, das Whisper erwartet) und schreibt eine temporäre WAV-Datei.
public sealed class MicrophoneRecorder
{
    private WaveIn? _in;
    private WaveFileWriter? _writer;
    private string? _path;
    private TaskCompletionSource<string?>? _stopped;

    public bool IsRecording => _in is not null;

    public void Start()
    {
        if (_in is not null) return;
        var format = new WaveFormat(16000, 16, 1);
        _path = Path.Combine(Path.GetTempPath(), $"wc_rec_{Guid.NewGuid():N}.wav");
        _writer = new WaveFileWriter(_path, format);
        _stopped = new TaskCompletionSource<string?>();

        var waveIn = new WaveIn { WaveFormat = format, BufferMilliseconds = 100 };
        waveIn.DataAvailable += (_, e) => _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        waveIn.RecordingStopped += (_, _) =>
        {
            _writer?.Dispose(); _writer = null;
            waveIn.Dispose(); _in = null;
            _stopped?.TrySetResult(_path);
        };
        _in = waveIn;
        try { waveIn.StartRecording(); }
        catch { _in = null; _writer?.Dispose(); _writer = null; waveIn.Dispose(); throw; }
    }

    /// Beendet die Aufnahme und liefert den Pfad der WAV-Datei.
    public async Task<string?> StopAsync()
    {
        if (_in is null || _stopped is null) return null;
        var done = _stopped.Task;
        _in.StopRecording();
        return await done;
    }
}
