#nullable enable

// Unity targets .NET Standard 2.1, which lacks System.Runtime.CompilerServices.IsExternalInit.
// Declaring it here enables C# 9 init-only properties and records in this assembly.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
