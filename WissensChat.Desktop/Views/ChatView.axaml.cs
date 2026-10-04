using Avalonia.Controls;
using Avalonia.Threading;
using WissensChat.Desktop.ViewModels;

namespace WissensChat.Desktop.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _vm;

    public ChatView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.ScrollRequested -= OnScrollRequested;
        _vm = DataContext as ChatViewModel;
        if (_vm is not null) _vm.ScrollRequested += OnScrollRequested;
    }

    private void OnScrollRequested() => Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd());
}
