// Polyfill fuer C#-9 'init'-Setter auf .NET Standard 2.1 (Unity 6).
// Der C#-Compiler erwartet einen Typ System.Runtime.CompilerServices.IsExternalInit
// wenn 'init'-Setter genutzt werden — dieser Typ ist erst ab .NET 5 in der BCL.
// Diese leere internal-Klasse erfuellt die Compiler-Anforderung ohne weitere Effekte.

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
