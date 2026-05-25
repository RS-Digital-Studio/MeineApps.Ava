// Macht 'internal'-Member der Domain-Assembly für die Test-Assembly sichtbar.
// Wird gebraucht für CardInstance.ApplyLevelUp, RuneInstance.ApplyLevelUp etc.,
// die per Design intern bleiben sollen, aber von Tests verifiziert werden.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ArcaneKingdom.Domain.Tests")]
