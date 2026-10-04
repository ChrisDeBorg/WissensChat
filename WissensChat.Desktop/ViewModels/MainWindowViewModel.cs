namespace WissensChat.Desktop.ViewModels;

public sealed class MainWindowViewModel
{
    public LibraryViewModel Library { get; }
    public SearchViewModel Search { get; }
    public ChatViewModel Chat { get; }

    public MainWindowViewModel(LibraryViewModel library, SearchViewModel search, ChatViewModel chat)
    {
        Library = library; Search = search; Chat = chat;
        Library.Refresh();
    }
}
