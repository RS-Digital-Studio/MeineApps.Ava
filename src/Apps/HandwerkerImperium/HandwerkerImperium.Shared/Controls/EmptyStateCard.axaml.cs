using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using HandwerkerImperium.Icons;

namespace HandwerkerImperium.Controls;

/// <summary>
/// Wiederverwendbarer Empty-State mit GameIcon, Titel, Untertitel (Meister-Hans-Voice) und
/// optionalem Action-Button. Konsistent über Achievement / Tournament / Crafting / Research / LiveEvent.
/// </summary>
public partial class EmptyStateCard : UserControl
{
    public static readonly StyledProperty<GameIconKind> IconProperty =
        AvaloniaProperty.Register<EmptyStateCard, GameIconKind>(nameof(Icon), GameIconKind.Trophy);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyStateCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<EmptyStateCard, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<string> ActionTextProperty =
        AvaloniaProperty.Register<EmptyStateCard, string>(nameof(ActionText), string.Empty);

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyStateCard, ICommand?>(nameof(ActionCommand));

    public GameIconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public EmptyStateCard()
    {
        InitializeComponent();
        UpdateBindings();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty ||
            change.Property == TitleProperty ||
            change.Property == SubtitleProperty ||
            change.Property == ActionTextProperty ||
            change.Property == ActionCommandProperty)
        {
            UpdateBindings();
        }
    }

    private void UpdateBindings()
    {
        if (IconElement != null)
            IconElement.Kind = Icon;

        if (TitleText != null)
        {
            TitleText.Text = Title;
            TitleText.IsVisible = !string.IsNullOrEmpty(Title);
        }

        if (SubtitleText != null)
        {
            SubtitleText.Text = Subtitle;
            SubtitleText.IsVisible = !string.IsNullOrEmpty(Subtitle);
        }

        if (ActionButton != null)
        {
            ActionButton.Content = ActionText;
            ActionButton.Command = ActionCommand;
            ActionButton.IsVisible = !string.IsNullOrEmpty(ActionText) && ActionCommand != null;
        }
    }
}
