using Avalonia;
using Avalonia.Controls;
using FitnessRechner.ViewModels;

namespace FitnessRechner.Views;

public partial class ActivityView : UserControl
{
    public ActivityView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        try
        {
            if (DataContext is ActivityViewModel vm)
                await vm.OnAppearingAsync();
        }
        catch (Exception)
        {
            // async void darf keine Exception werfen
        }
    }
}
