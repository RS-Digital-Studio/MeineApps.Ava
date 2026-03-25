using Avalonia;
using Avalonia.Controls;
using FitnessRechner.ViewModels;

namespace FitnessRechner.Views;

public partial class RecipeView : UserControl
{
    public RecipeView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        try
        {
            if (DataContext is RecipeViewModel vm)
                await vm.OnAppearingAsync();
        }
        catch (Exception)
        {
            // async void darf keine Exception werfen
        }
    }
}
