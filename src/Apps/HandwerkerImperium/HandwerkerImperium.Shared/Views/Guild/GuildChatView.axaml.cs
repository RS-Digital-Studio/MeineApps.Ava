using Avalonia.Controls;
using Avalonia.Threading;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Guild;

public partial class GuildChatView : UserControl
{
    private ScrollViewer? _scrollViewer;

    public GuildChatView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _scrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");
        ScrollToBottom();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Bei neuen Nachrichten nach unten scrollen
        Dispatcher.UIThread.Post(ScrollToBottom, DispatcherPriority.Background);
    }

    private void ScrollToBottom()
    {
        _scrollViewer?.ScrollToEnd();
    }
}
