using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using WissensChat.Core;
using WissensChat.Desktop.Audio;

namespace WissensChat.Desktop.ViewModels;

public partial class ChatMessageVm(string role, string text, bool isUser) : ObservableObject
{
    public string Role { get; } = role;
    public bool IsUser { get; } = isUser;
    [ObservableProperty] private string _text = text;
    [ObservableProperty] private string _sources = "";
}

public partial class ChatViewModel : ObservableObject
{
    private readonly RagService _rag;
    private readonly MicrophoneRecorder _mic;
    private readonly WhisperTranscriber _stt;
    private readonly SpeechPlayer _speaker;
    private readonly List<ChatMessage> _history = [];
    private CancellationTokenSource? _cts;

    public ChatViewModel(RagService rag, MicrophoneRecorder mic, WhisperTranscriber stt, SpeechPlayer speaker)
    {
        _rag = rag; _mic = mic; _stt = stt; _speaker = speaker;
        _speaker.Error += msg => Dispatcher.UIThread.Post(() => VoiceStatus = msg);
    }

    public ObservableCollection<ChatMessageVm> Messages { get; } = [];
    public event Action? ScrollRequested;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SendCommand))] private string _input = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(CancelCommand))] private bool _isBusy;
    [ObservableProperty] private bool _speakAnswers = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MicLabel))] private bool _isRecording;
    [ObservableProperty] private string _voiceStatus = "";

    public string MicLabel => IsRecording ? "■ Aufnahme beenden" : "● Sprechen";

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        _speaker.Stop();
        var question = Input.Trim();
        Input = "";
        Messages.Add(new ChatMessageVm("Du", question, true));
        var reply = new ChatMessageVm("KI", "", false);
        Messages.Add(reply);
        ScrollRequested?.Invoke();

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            var answer = await _rag.AskAsync(_history, question, _cts.Token);
            var sb = new StringBuilder();
            var speechBuffer = new StringBuilder();
            await foreach (var token in answer.Tokens.WithCancellation(_cts.Token))
            {
                sb.Append(token);
                reply.Text = sb.ToString();
                ScrollRequested?.Invoke();

                if (SpeakAnswers)
                {
                    speechBuffer.Append(token);
                    foreach (var sentence in SpeechText.TakeSentences(speechBuffer))
                        _speaker.Enqueue(sentence);
                }
            }

            if (SpeakAnswers)
            {
                var rest = SpeechText.Clean(speechBuffer.ToString());
                if (rest.Length > 0) _speaker.Enqueue(rest);
            }

            if (answer.Sources.Count > 0)
                reply.Sources = "Quellen: " + string.Join("  ·  ",
                    answer.Sources.Select((h, i) => $"[{i + 1}] {h.Source}"));

            _history.Add(new ChatMessage(ChatRole.User, question));
            _history.Add(new ChatMessage(ChatRole.Assistant, sb.ToString()));
        }
        catch (OperationCanceledException) { reply.Text += " [abgebrochen]"; _speaker.Stop(); }
        catch (Exception ex)
        {
            _speaker.Stop();
            reply.Text = $"Fehler: {ex.Message}\n(Läuft Ollama, und sind beide Modelle geladen?)";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }
    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(Input);

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void StopSpeaking() => _speaker.Stop();

    [RelayCommand]
    private void NewChat() { _speaker.Stop(); _history.Clear(); Messages.Clear(); }

    /// Push-to-talk per Klick: 1. Klick startet, 2. Klick beendet, erkennt und sendet.
    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (!IsRecording)
        {
            _speaker.Stop(); // Antwort nicht ins Mikrofon sprechen lassen
            try
            {
                _mic.Start();
                IsRecording = true;
                VoiceStatus = "Aufnahme läuft … zum Beenden erneut klicken.";
            }
            catch (Exception ex) { VoiceStatus = $"Mikrofon-Fehler: {ex.Message}"; }
            return;
        }

        IsRecording = false;
        var wav = await _mic.StopAsync();
        if (wav is null) return;

        string text;
        try
        {
            if (new FileInfo(wav).Length < 16000 * 2 * 0.5) { VoiceStatus = "Aufnahme zu kurz."; return; }
            var progress = new Progress<string>(s => VoiceStatus = s);
            text = await _stt.TranscribeAsync(wav, progress);
        }
        catch (Exception ex) { VoiceStatus = $"Spracherkennung fehlgeschlagen: {ex.Message}"; return; }
        finally { try { File.Delete(wav); } catch { } }

        if (string.IsNullOrWhiteSpace(text)) { VoiceStatus = "Nichts erkannt."; return; }
        VoiceStatus = "";
        Input = text;
        if (SendCommand.CanExecute(null)) await SendCommand.ExecuteAsync(null);
    }
}
