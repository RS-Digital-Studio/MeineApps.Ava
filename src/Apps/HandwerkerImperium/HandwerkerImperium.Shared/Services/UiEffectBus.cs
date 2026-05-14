using System;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Standard-Implementierung von <see cref="IUiEffectBus"/>. Reiner Event-Multiplexer
/// ohne eigenen Zustand — die Raise-Methoden leiten 1:1 an die Events weiter.
/// </summary>
public sealed class UiEffectBus : IUiEffectBus
{
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;
    public event Action<CeremonyType, string, string>? CeremonyRequested;

    public void RaiseFloatingText(string text, string category)
        => FloatingTextRequested?.Invoke(text, category);

    public void RaiseCelebration()
        => CelebrationRequested?.Invoke();

    public void RaiseCeremony(CeremonyType type, string title, string subtitle)
        => CeremonyRequested?.Invoke(type, title, subtitle);
}
