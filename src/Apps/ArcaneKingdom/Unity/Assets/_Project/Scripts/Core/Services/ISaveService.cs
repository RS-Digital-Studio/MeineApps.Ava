#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Abstraktion ueber den Save-Provider. Cloud-First (Firebase RTDB) mit lokalem JSON-Fallback.
    /// </summary>
    /// <typeparam name="TSave">Save-Datentyp (z.B. PlayerSave).</typeparam>
    public interface ISaveService<TSave> where TSave : class
    {
        UniTask<Result<TSave>> LoadAsync(CancellationToken ct = default);
        UniTask<Result> SaveAsync(TSave save, CancellationToken ct = default);

        /// <summary>
        /// Optimistische Mutation: lokaler State wird sofort aktualisiert, Server-Sync laeuft async.
        /// Bei Konflikt gewinnt der Server, lokale Mutation wird verworfen (mit Event-Notification).
        /// </summary>
        UniTask<Result<TSave>> MutateAsync(Func<TSave, TSave> mutation, CancellationToken ct = default);
    }
}
