#nullable enable

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Lets records and <c>init</c> properties compile on .NET Standard 2.1, which Unity uses.
    /// The compiler only needs this type to exist.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
