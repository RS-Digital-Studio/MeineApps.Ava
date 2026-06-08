using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game.Greybox
{
    /// <summary>
    /// Geteilter Laufzeit-Kontext (DI-Singleton): das aktuelle, Unity-freie Sim-Modell + das Balancing.
    /// Alle P0-§3-Services arbeiten auf diesem einen Zustand.
    /// </summary>
    public sealed class GreyboxSession
    {
        public IdleBalancing Balancing { get; }
        public GreyboxSimState State { get; }

        public GreyboxSession(IdleBalancing balancing, GreyboxSimState state)
        {
            Balancing = balancing;
            State = state;
        }
    }
}
