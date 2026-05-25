// Polyfill für C#-9 'init'-Setter auf .NET Standard 2.1 (Unity 6).
// Der C#-Compiler erwartet einen Typ System.Runtime.CompilerServices.IsExternalInit
// wenn 'init'-Setter genutzt werden — dieser Typ ist erst ab .NET 5 in der BCL.
//
// MUSS public sein, damit Domain/Game/UI/Bootstrap-Assemblies den Typ über die
// Core-asmdef-Reference transitiv sehen können (internal-Klassen sind
// assembly-lokal und reichen NICHT). Der Compiler ist nicht waehlerisch
// hinsichtlich der Sichtbarkeit dieses Markers.

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit
    {
    }
}
#endif
