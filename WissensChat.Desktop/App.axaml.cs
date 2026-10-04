using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using WissensChat.Core;
using WissensChat.Desktop.Audio;
using WissensChat.Desktop.Services;
using WissensChat.Desktop.ViewModels;
using WissensChat.Desktop.Views;

namespace WissensChat.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var provider = BuildServices();
            var window = new MainWindow { DataContext = provider.GetRequiredService<MainWindowViewModel>() };
            provider.GetRequiredService<FileDialogService>().Owner = window;
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var settings = new AppSettings(); // später aus Datei/Einstellungsdialog laden
        var services = new ServiceCollection();

        services.AddSingleton(settings);
        services.AddSingleton<IChatClient>(_ =>
            new OllamaApiClient(new Uri(settings.OllamaUrl), settings.ChatModel));
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OllamaApiClient(new Uri(settings.OllamaUrl), settings.EmbeddingModel));

        services.AddSingleton<ChunkStore>();
        services.AddSingleton<IngestionService>();
        services.AddSingleton<RagService>();
        services.AddSingleton<FileDialogService>();

        // Audio
        services.AddSingleton(new AudioSettings());
        services.AddSingleton<MicrophoneRecorder>();
        services.AddSingleton<WhisperTranscriber>();
        services.AddSingleton<TtsEngine>();
        services.AddSingleton<SpeechPlayer>();

        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ChunkStore>().Initialize();
        return provider;
    }
}
