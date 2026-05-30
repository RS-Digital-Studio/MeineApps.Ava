#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Abstraktion über den Save-Provider.
    /// AKTUELL: rein lokales JSON (Atomic-Write + Backup-Rotation). Cloud-Sync (Firebase RTDB als
    /// Source-of-Truth, Konflikt-Aufloesung via LastSavedAtUtc) ist vorgesehen, sobald das Firebase
    /// Unity SDK eingebunden ist (siehe FIREBASE_SETUP.md / Roadmap). Bis dahin gibt es KEIN Cloud-Backup.
    /// </summary>
    /// <typeparam name="TSave">Save-Datentyp (z.B. PlayerSave).</typeparam>
    public interface ISaveService<TSave> where TSave : class
    {
        UniTask<Result<TSave>> LoadAsync(CancellationToken ct = default);
        UniTask<Result> SaveAsync(TSave save, CancellationToken ct = default);

        /// <summary>
        /// Atomare Read-Modify-Write-Mutation: Load + mutation + Save laufen serialisiert unter einem
        /// Lock, sodass nebenlaeufige Mutationen keine Buchung verlieren/duplizieren. Die mutation-Lambda
        /// sollte ihre Vorbedingungen (z.B. Gold-Deckung) selbst pruefen und bei Fehlschlag den State
        /// unveraendert zurueckgeben. Sobald Cloud-Sync aktiv ist, gilt bei Konflikt Server-Wins.
        /// </summary>
        UniTask<Result<TSave>> MutateAsync(Func<TSave, TSave> mutation, CancellationToken ct = default);
    }
}
