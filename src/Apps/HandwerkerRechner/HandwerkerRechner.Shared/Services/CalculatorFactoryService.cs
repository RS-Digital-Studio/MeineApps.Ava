using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.ViewModels.Floor;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Services;

/// <summary>
/// Zentrales Route→Factory-Mapping für alle 19 Calculator-VMs.
/// Wird als Singleton registriert und kapselt die IServiceProvider-Auflösung.
/// </summary>
public sealed class CalculatorFactoryService : ICalculatorFactoryService
{
    private readonly Dictionary<string, Func<ObservableObject>> _factories;

    public CalculatorFactoryService(IServiceProvider serviceProvider)
    {
        // Alle 19 Calculator-VMs als Transient über den ServiceProvider auflösen
        _factories = new Dictionary<string, Func<ObservableObject>>
        {
            // Free Floor (5)
            ["TileCalculatorPage"] = () => Resolve<TileCalculatorViewModel>(serviceProvider),
            ["WallpaperCalculatorPage"] = () => Resolve<WallpaperCalculatorViewModel>(serviceProvider),
            ["PaintCalculatorPage"] = () => Resolve<PaintCalculatorViewModel>(serviceProvider),
            ["FlooringCalculatorPage"] = () => Resolve<FlooringCalculatorViewModel>(serviceProvider),
            ["ConcretePage"] = () => Resolve<ConcreteCalculatorViewModel>(serviceProvider),

            // Premium (11)
            ["DrywallPage"] = () => Resolve<DrywallViewModel>(serviceProvider),
            ["ElectricalPage"] = () => Resolve<ElectricalViewModel>(serviceProvider),
            ["MetalPage"] = () => Resolve<MetalViewModel>(serviceProvider),
            ["GardenPage"] = () => Resolve<GardenViewModel>(serviceProvider),
            ["RoofSolarPage"] = () => Resolve<RoofSolarViewModel>(serviceProvider),
            ["StairsPage"] = () => Resolve<StairsViewModel>(serviceProvider),
            ["PlasterPage"] = () => Resolve<PlasterViewModel>(serviceProvider),
            ["ScreedPage"] = () => Resolve<ScreedViewModel>(serviceProvider),
            ["InsulationPage"] = () => Resolve<InsulationViewModel>(serviceProvider),
            ["CableSizingPage"] = () => Resolve<CableSizingViewModel>(serviceProvider),
            ["GroutPage"] = () => Resolve<GroutViewModel>(serviceProvider),

            // Profi-Werkzeuge (3)
            ["HourlyRatePage"] = () => Resolve<HourlyRateViewModel>(serviceProvider),
            ["MaterialComparePage"] = () => Resolve<MaterialCompareViewModel>(serviceProvider),
            ["AreaMeasurePage"] = () => Resolve<AreaMeasureViewModel>(serviceProvider),
        };
    }

    public ObservableObject? Create(string route)
        => _factories.TryGetValue(route, out var factory) ? factory() : null;

    public bool HasRoute(string route) => _factories.ContainsKey(route);

    private static T Resolve<T>(IServiceProvider sp) where T : notnull
        => (T)sp.GetService(typeof(T))!;
}
