namespace WissensChat.Desktop.Audio;

public sealed class AudioSettings
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WissensChat");

    /// Whisper-Sprache ("de", "en", "auto" …)
    public string Language { get; set; } = "de";

    /// Name aus Whisper.net.Ggml.GgmlType: Tiny, Base, Small, Medium, LargeV3Turbo …
    /// "Small" ist für Deutsch ein guter Kompromiss (~470 MB), "Base" ist schneller, aber ungenauer.
    public string WhisperModel { get; set; } = "Small";
    public string ModelDirectory { get; set; } = Path.Combine(Root, "models");

    /// Optional: Piper (bessere Stimme). Fehlt eine der Dateien, wird die Windows-Stimme genutzt.
    public string PiperExe { get; set; } = Path.Combine(Root, "piper", "piper.exe");
    public string PiperVoice { get; set; } = Path.Combine(Root, "piper", "voices", "de_DE-thorsten-medium.onnx");
    public bool PiperAvailable => File.Exists(PiperExe) && File.Exists(PiperVoice);
}
