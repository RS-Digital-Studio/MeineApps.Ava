using VContainer;
using VContainer.Unity;

namespace BomberBlast.Bootstrap
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // TODO: Services beim Domain-/Game-Port registrieren
        }
    }
}
