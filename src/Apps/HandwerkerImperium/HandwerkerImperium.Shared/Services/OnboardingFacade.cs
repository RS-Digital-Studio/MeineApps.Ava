using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IOnboardingFacade"/>. Singleton, kein State.
/// </summary>
public sealed class OnboardingFacade : IOnboardingFacade
{
    public IFtueService Ftue { get; }
    public IStoryService? Story { get; }
    public IContextualHintService Hint { get; }
    public IWelcomeBackService WelcomeBack { get; }
    public IReferralService? Referral { get; }

    public OnboardingFacade(
        IFtueService ftue,
        IContextualHintService hint,
        IWelcomeBackService welcomeBack,
        IStoryService? story = null,
        IReferralService? referral = null)
    {
        Ftue = ftue;
        Hint = hint;
        WelcomeBack = welcomeBack;
        Story = story;
        Referral = referral;
    }
}
